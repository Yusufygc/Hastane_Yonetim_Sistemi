using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.CancelAppointment;

public sealed class CancelAppointmentHandler(
    IAppointmentRepository appointments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    AppointmentSettings settings,
    TimeProvider clock) : ICommandHandler<CancelAppointmentCommand, Result>
{
    public async ValueTask<Result> Handle(CancelAppointmentCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId)
        {
            return HospitalErrors.Forbidden;
        }

        var appointment = await appointments.GetByIdAsync(command.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return AppointmentErrors.NotFound;
        }

        var accessError = await AppointmentAccess.CheckViewAccessAsync(
            appointment.PatientId, appointment.DoctorId, appointment.HospitalId, hospitals, currentUser, cancellationToken);

        if (accessError is not null)
        {
            return accessError;
        }

        if (!appointment.Allows(AppointmentAction.Cancel))
        {
            return AppointmentErrors.InvalidTransition(appointment.State.Label);
        }

        var now = clock.GetUtcNow();

        // Süre sınırı yalnız hastaya uygulanır; doktor ve yönetici bu sınıra tabi değildir.
        if (currentUser.Role == RoleNames.Patient && appointment.IsWithinCancellationDeadline(now, settings.CancellationDeadline))
        {
            return AppointmentErrors.CancellationDeadlinePassed(settings.CancellationDeadlineHours);
        }

        appointment.Cancel(actorId, command.Reason, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
