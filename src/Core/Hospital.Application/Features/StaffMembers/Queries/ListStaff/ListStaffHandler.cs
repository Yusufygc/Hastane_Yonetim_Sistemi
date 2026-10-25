using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Parameters;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.StaffMembers.Queries.ListStaff;

public sealed class ListStaffHandler(
    IHospitalRepository hospitals,
    IStaffReader reader,
    ICurrentUser currentUser) : IQueryHandler<ListStaffQuery, Result<PagedList<StaffDto>>>
{
    public async ValueTask<Result<PagedList<StaffDto>>> Handle(ListStaffQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        if (query.HospitalId is { } hospitalId)
        {
            var hospital = await hospitals.GetByIdAsync(hospitalId, includeWorkingHours: false, cancellationToken);
            if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
            {
                return error;
            }
        }

        return Result.Success(await reader.ListAsync(scope, query.HospitalId, PageRequest.From(query.Page, query.PageSize), cancellationToken));
    }
}
