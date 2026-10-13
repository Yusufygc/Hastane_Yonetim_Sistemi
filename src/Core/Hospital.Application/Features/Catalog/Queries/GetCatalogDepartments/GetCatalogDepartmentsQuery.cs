using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogDepartments;

public sealed record GetCatalogDepartmentsQuery(Guid HospitalId) : IQuery<Result<IReadOnlyList<CatalogDepartmentDto>>>;
