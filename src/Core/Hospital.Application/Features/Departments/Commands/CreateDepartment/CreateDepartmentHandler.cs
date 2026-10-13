using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;
using Mediator;

namespace Hospital.Application.Features.Departments.Commands.CreateDepartment;

public sealed class CreateDepartmentHandler(
    IHospitalRepository hospitals,
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateDepartmentCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(command.HospitalId, includeWorkingHours: false, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
        {
            return error;
        }

        if (await departments.NameExistsAsync(hospital!.Id, command.Name.Trim(), null, cancellationToken))
        {
            return DepartmentErrors.NameTaken;
        }

        var department = Department.Create(hospital.Id, command.Name);
        departments.Add(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return department.Id;
    }
}
