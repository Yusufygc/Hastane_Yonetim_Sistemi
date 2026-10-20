using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveMyAvailability;

public sealed class RemoveMyAvailabilityHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<RemoveMyAvailabilityCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveMyAvailabilityCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.Availabilities, includeWorkingHours: false, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        if (!loaded.Value.Doctor.RemoveAvailability(command.Id))
        {
            return DoctorErrors.AvailabilityNotFound;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
