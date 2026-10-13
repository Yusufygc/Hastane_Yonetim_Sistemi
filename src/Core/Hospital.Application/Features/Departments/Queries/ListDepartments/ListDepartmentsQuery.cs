using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Queries.ListDepartments;

public sealed record ListDepartmentsQuery(Guid HospitalId) : IQuery<Result<IReadOnlyList<DepartmentDto>>>;
