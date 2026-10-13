using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogServices;

public sealed record GetCatalogServicesQuery(Guid HospitalId) : IQuery<Result<IReadOnlyList<CatalogServiceDto>>>;
