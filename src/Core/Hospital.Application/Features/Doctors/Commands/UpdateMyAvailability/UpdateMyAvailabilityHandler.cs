using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.UpdateMyAvailability;

public sealed class UpdateMyAvailabilityHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateMyAvailabilityCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateMyAvailabilityCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.Availabilities, includeWorkingHours: true, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (doctor, hospital) = loaded.Value;

        if (doctor.FindAvailability(command.Id) is null)
        {
            return DoctorErrors.AvailabilityNotFound;
        }

        if (!DoctorAvailability.FitsWithinHospitalHours(hospital.WorkingHours, command.Day, command.StartTime, command.EndTime))
        {
            return DoctorErrors.AvailabilityOutsideHours;
        }

        if (doctor.FindOverlappingAvailability(
                command.Day, command.StartTime, command.EndTime, command.ValidFrom, command.ValidTo, ignoreId: command.Id) is not null)
        {
            return DoctorErrors.AvailabilityOverlap;
        }

        doctor.UpdateAvailability(
            command.Id, command.Day, command.StartTime, command.EndTime, command.ValidFrom, command.ValidTo, hospital.WorkingHours);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
