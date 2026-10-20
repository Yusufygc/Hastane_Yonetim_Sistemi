using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveMyService;

public sealed class RemoveMyServiceHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<RemoveMyServiceCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveMyServiceCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.Services, includeWorkingHours: false, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var doctor = loaded.Value.Doctor;
        var link = doctor.FindService(command.ServiceId);

        if (link is null)
        {
            return DoctorErrors.ServiceLinkNotFound;
        }

        if (link.Status == DoctorServiceStatus.Approved)
        {
            return DoctorErrors.ServiceApprovedLocked;
        }

        doctor.RemoveService(command.ServiceId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
