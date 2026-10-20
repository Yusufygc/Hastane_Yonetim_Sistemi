using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.AddMyAvailability;

public sealed class AddMyAvailabilityHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<AddMyAvailabilityCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(AddMyAvailabilityCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.Availabilities, includeWorkingHours: true, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (doctor, hospital) = loaded.Value;

        if (!DoctorAvailability.FitsWithinHospitalHours(hospital.WorkingHours, command.Day, command.StartTime, command.EndTime))
        {
            return DoctorErrors.AvailabilityOutsideHours;
        }

        if (doctor.FindOverlappingAvailability(command.Day, command.StartTime, command.EndTime, command.ValidFrom, command.ValidTo) is not null)
        {
            return DoctorErrors.AvailabilityOverlap;
        }

        var availability = doctor.AddAvailability(
            command.Day, command.StartTime, command.EndTime, command.ValidFrom, command.ValidTo, hospital.WorkingHours);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return availability.Id;
    }
}
