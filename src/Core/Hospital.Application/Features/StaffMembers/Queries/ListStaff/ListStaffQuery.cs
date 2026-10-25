using Hospital.Application.Parameters;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.StaffMembers.Queries.ListStaff;

public sealed record ListStaffQuery(Guid? HospitalId, int? Page = null, int? PageSize = null)
    : IQuery<Result<PagedList<StaffDto>>>;
