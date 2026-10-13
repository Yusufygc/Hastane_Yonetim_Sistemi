using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Commands.UpdateService;

public sealed class UpdateServiceHandler(
    IHospitalRepository hospitals,
    IDepartmentRepository departments,
    IServiceRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateServiceCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var service = await services.GetByIdAsync(command.Id, cancellationToken);
        if (service is null)
        {
            return scope is null ? HospitalErrors.Forbidden : ServiceErrors.NotFound;
        }

        var hospital = await hospitals.GetByIdAsync(service.HospitalId, includeWorkingHours: false, cancellationToken);
        if (HospitalErrors.CheckAccess(scope, hospital) is not null)
        {
            // Kapsam dışı hizmetin varlığı sızdırılmaz.
            return scope is null ? HospitalErrors.Forbidden : ServiceErrors.NotFound;
        }

        if (!await ServiceDepartmentCheck.IsValidAsync(departments, service.HospitalId, command.DepartmentId, cancellationToken))
        {
            return ServiceErrors.DepartmentInvalid;
        }

        if (await services.NameExistsAsync(service.HospitalId, command.Name.Trim(), service.Id, cancellationToken))
        {
            return ServiceErrors.NameTaken;
        }

        service.Update(command.DepartmentId, command.Name, command.DurationMinutes, command.Price, command.Currency);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
