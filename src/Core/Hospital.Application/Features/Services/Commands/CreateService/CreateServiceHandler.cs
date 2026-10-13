using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;
using Mediator;

namespace Hospital.Application.Features.Services.Commands.CreateService;

public sealed class CreateServiceHandler(
    IHospitalRepository hospitals,
    IDepartmentRepository departments,
    IServiceRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateServiceCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateServiceCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(command.HospitalId, includeWorkingHours: false, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
        {
            return error;
        }

        if (!await ServiceDepartmentCheck.IsValidAsync(departments, hospital!.Id, command.DepartmentId, cancellationToken))
        {
            return ServiceErrors.DepartmentInvalid;
        }

        if (await services.NameExistsAsync(hospital.Id, command.Name.Trim(), null, cancellationToken))
        {
            return ServiceErrors.NameTaken;
        }

        var service = Service.Create(
            hospital.Id, command.DepartmentId, command.Name, command.DurationMinutes, command.Price, command.Currency);

        services.Add(service);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return service.Id;
    }
}
