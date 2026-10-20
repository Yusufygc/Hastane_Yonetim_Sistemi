using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Features.Services;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.AssignDoctorServices;

public sealed class AssignDoctorServicesHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IServiceRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<AssignDoctorServicesCommand, Result>
{
    public async ValueTask<Result> Handle(AssignDoctorServicesCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadForAdminAsync(
            command.DoctorId, DoctorInclude.Services, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (doctor, hospital) = loaded.Value;
        var wanted = command.ServiceIds.Distinct().ToList();

        foreach (var serviceId in wanted)
        {
            var service = await services.GetByIdAsync(serviceId, cancellationToken);
            if (service is not { IsActive: true } || service.HospitalId != hospital.Id)
            {
                return DoctorErrors.ServiceInvalid;
            }
        }

        var now = clock.GetUtcNow();
        var adminId = currentUser.UserId!.Value;

        var toRemove = doctor.Services
            .Where(link => link.Status == DoctorServiceStatus.Approved && !wanted.Contains(link.ServiceId))
            .Select(link => link.ServiceId)
            .ToList();

        foreach (var serviceId in toRemove)
        {
            doctor.RemoveService(serviceId);
        }

        foreach (var serviceId in wanted)
        {
            doctor.AssignService(serviceId, adminId, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
