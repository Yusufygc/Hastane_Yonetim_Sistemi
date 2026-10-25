using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Parameters;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListDoctorServiceRequests;

public sealed class ListDoctorServiceRequestsHandler(IDoctorReader reader, ICurrentUser currentUser)
    : IQueryHandler<ListDoctorServiceRequestsQuery, Result<PagedList<DoctorServiceRequestDto>>>
{
    public async ValueTask<Result<PagedList<DoctorServiceRequestDto>>> Handle(
        ListDoctorServiceRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        var status = string.IsNullOrWhiteSpace(query.Status) ? DoctorStatusFilter.Pending : query.Status.Trim().ToUpperInvariant();
        return Result.Success(await reader.ListServiceRequestsAsync(
            scope, status, PageRequest.From(query.Page, query.PageSize), cancellationToken));
    }
}
