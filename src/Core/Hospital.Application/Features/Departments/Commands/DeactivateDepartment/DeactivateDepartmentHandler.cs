using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Commands.DeactivateDepartment;

public sealed class DeactivateDepartmentHandler(
    IHospitalRepository hospitals,
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<DeactivateDepartmentCommand, Result>
{
    public async ValueTask<Result> Handle(DeactivateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var department = await departments.GetByIdAsync(command.Id, cancellationToken);
        if (department is null)
        {
            return scope is null ? HospitalErrors.Forbidden : DepartmentErrors.NotFound;
        }

        var hospital = await hospitals.GetByIdAsync(department.HospitalId, includeWorkingHours: false, cancellationToken);
        if (HospitalErrors.CheckAccess(scope, hospital) is not null)
        {
            return scope is null ? HospitalErrors.Forbidden : DepartmentErrors.NotFound;
        }

        if (await departments.HasActiveServicesAsync(department.Id, cancellationToken))
        {
            return DepartmentErrors.HasActiveServices;
        }

        department.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
