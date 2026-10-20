using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Features.Services;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.ApproveDoctorService;

public sealed class ApproveDoctorServiceHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IServiceRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<ApproveDoctorServiceCommand, Result>
{
    public async ValueTask<Result> Handle(ApproveDoctorServiceCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadForAdminAsync(
            command.DoctorId, DoctorInclude.Services, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (doctor, hospital) = loaded.Value;

        if (doctor.FindService(command.ServiceId) is null)
        {
            return DoctorErrors.ServiceLinkNotFound;
        }

        var service = await services.GetByIdAsync(command.ServiceId, cancellationToken);
        if (service is not { IsActive: true } || service.HospitalId != hospital.Id)
        {
            return DoctorErrors.ServiceInvalid;
        }

        doctor.ApproveService(command.ServiceId, currentUser.UserId!.Value, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
