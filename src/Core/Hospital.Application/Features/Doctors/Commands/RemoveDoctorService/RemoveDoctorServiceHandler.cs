using Hospital.Application.Features.Appointments;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveDoctorService;

public sealed class RemoveDoctorServiceHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<RemoveDoctorServiceCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveDoctorServiceCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadForAdminAsync(
            command.DoctorId, DoctorInclude.Services, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var doctor = loaded.Value.Doctor;

        if (doctor.FindService(command.ServiceId) is null)
        {
            return DoctorErrors.ServiceLinkNotFound;
        }

        // Gelecekte aktif randevusu olan hizmet doktordan kaldırılamaz.
        if (await appointments.DoctorHasFutureActiveAsync(doctor.Id, command.ServiceId, clock.GetUtcNow(), cancellationToken))
        {
            return DoctorErrors.ServiceHasFutureAppointments;
        }

        doctor.RemoveService(command.ServiceId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
