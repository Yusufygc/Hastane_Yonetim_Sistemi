using Hospital.Application.Exceptions;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.CreateAppointment;

/// <summary>
/// Doğrulama sırası: doktor/şube/hizmet tutarlı → hizmet onaylı sunuluyor → aralık üretilen bir slot → hastanın aynı aralıkta
/// başka aktif randevusu yok → kayıt (PENDING + snapshot). Ön kontroller hızlı ve anlaşılır hata içindir; eşzamanlı
/// yarışı veritabanı exclusion kısıtları kapatır (kaybeden istek <c>appointment.slot_taken</c> 409 alır).
/// Hasta hesabının aktifliği her istekte JWT doğrulamasında (güvenlik damgası) denetlenir.
/// </summary>
public sealed class CreateAppointmentHandler(
    SlotAvailabilityService slots,
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CreateAppointmentCommand, Result<Guid>>
{
    private const string DoctorConstraint = "ex_doctor_no_overlap";
    private const string PatientConstraint = "ex_patient_no_overlap";

    public async ValueTask<Result<Guid>> Handle(CreateAppointmentCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } patientId)
        {
            return HospitalErrors.Forbidden;
        }

        var loaded = await slots.LoadAsync(command.DoctorId, command.ServiceId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var context = loaded.Value;
        var requestedStart = command.StartAt.ToUniversalTime();

        var available = await slots.ComputeSlotsAsync(context, context.LocalDateOf(requestedStart), cancellationToken);
        var matching = available.Where(s => s.Start == requestedStart).ToList();

        if (matching.Count == 0)
        {
            return AppointmentErrors.SlotUnavailable;
        }

        var slot = matching[0];

        if (await appointments.PatientHasOverlapAsync(patientId, slot.Start, slot.End, cancellationToken))
        {
            return AppointmentErrors.PatientOverlap;
        }

        var appointment = Appointment.Request(
            patientId,
            context.Doctor.Id,
            context.Hospital.Id,
            ServiceSnapshot.From(context.Service),
            slot.Start,
            slot.End,
            command.PatientNote,
            clock.GetUtcNow());

        appointments.Add(appointment);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DataConflictException ex) when (ex.ConstraintName == DoctorConstraint)
        {
            return AppointmentErrors.SlotTaken;
        }
        catch (DataConflictException ex) when (ex.ConstraintName == PatientConstraint)
        {
            return AppointmentErrors.PatientOverlap;
        }

        return appointment.Id;
    }
}
