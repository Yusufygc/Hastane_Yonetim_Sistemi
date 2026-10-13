using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Queries.ListDepartments;

public sealed class ListDepartmentsHandler(
    IHospitalRepository hospitals,
    IDepartmentReader reader,
    ICurrentUser currentUser) : IQueryHandler<ListDepartmentsQuery, Result<IReadOnlyList<DepartmentDto>>>
{
    public async ValueTask<Result<IReadOnlyList<DepartmentDto>>> Handle(ListDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(query.HospitalId, includeWorkingHours: false, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
        {
            return error;
        }

        return Result.Success(await reader.ListAsync(hospital!.Id, cancellationToken));
    }
}
