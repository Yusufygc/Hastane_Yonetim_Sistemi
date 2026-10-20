using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.StaffMembers.Queries.ListStaff;

public sealed record ListStaffQuery(Guid? HospitalId) : IQuery<Result<IReadOnlyList<StaffDto>>>;
