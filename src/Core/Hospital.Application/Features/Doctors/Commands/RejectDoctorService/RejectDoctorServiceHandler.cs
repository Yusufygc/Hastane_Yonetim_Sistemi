using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RejectDoctorService;

public sealed class RejectDoctorServiceHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<RejectDoctorServiceCommand, Result>
{
    public async ValueTask<Result> Handle(RejectDoctorServiceCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadForAdminAsync(
            command.DoctorId, DoctorInclude.Services, currentUser, doctors, departments, hospitals, cancellationToken);

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

        if (link.Status != DoctorServiceStatus.Pending)
        {
            return DoctorErrors.ServiceNotPending;
        }

        doctor.RejectService(command.ServiceId, currentUser.UserId!.Value, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
