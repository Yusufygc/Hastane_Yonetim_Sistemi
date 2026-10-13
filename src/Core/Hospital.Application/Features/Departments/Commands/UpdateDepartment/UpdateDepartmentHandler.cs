using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Commands.UpdateDepartment;

public sealed class UpdateDepartmentHandler(
    IHospitalRepository hospitals,
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateDepartmentCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateDepartmentCommand command, CancellationToken cancellationToken)
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
            // Kapsam dışı bölümün varlığı sızdırılmaz.
            return scope is null ? HospitalErrors.Forbidden : DepartmentErrors.NotFound;
        }

        if (await departments.NameExistsAsync(department.HospitalId, command.Name.Trim(), department.Id, cancellationToken))
        {
            return DepartmentErrors.NameTaken;
        }

        department.Rename(command.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
