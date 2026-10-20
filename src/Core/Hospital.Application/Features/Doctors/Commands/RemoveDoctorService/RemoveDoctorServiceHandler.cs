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
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<RemoveDoctorServiceCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveDoctorServiceCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadForAdminAsync(
            command.DoctorId, DoctorInclude.Services, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        if (!loaded.Value.Doctor.RemoveService(command.ServiceId))
        {
            return DoctorErrors.ServiceLinkNotFound;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
