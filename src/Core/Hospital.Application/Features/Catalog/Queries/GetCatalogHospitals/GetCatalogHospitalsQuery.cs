using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogHospitals;

public sealed record GetCatalogHospitalsQuery : IQuery<Result<IReadOnlyList<CatalogHospitalDto>>>;
