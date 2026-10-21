using Hospital.Application.Features.Appointments;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.AddMyTimeOff;

public sealed class AddMyTimeOffHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<AddMyTimeOffCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(AddMyTimeOffCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.TimeOffs, includeWorkingHours: false, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var doctor = loaded.Value.Doctor;
        var now = clock.GetUtcNow();

        if (command.EndAt <= now)
        {
            return DoctorErrors.TimeOffInPast;
        }

        if (doctor.FindOverlappingTimeOff(command.StartAt, command.EndAt) is not null)
        {
            return DoctorErrors.TimeOffOverlap;
        }

        // İzin, mevcut aktif randevuları geçersiz kılamaz.
        if (await appointments.DoctorHasActiveInRangeAsync(doctor.Id, command.StartAt, command.EndAt, cancellationToken))
        {
            return DoctorErrors.TimeOffHasAppointments;
        }

        var timeOff = doctor.AddTimeOff(command.StartAt, command.EndAt, command.Reason, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return timeOff.Id;
    }
}
