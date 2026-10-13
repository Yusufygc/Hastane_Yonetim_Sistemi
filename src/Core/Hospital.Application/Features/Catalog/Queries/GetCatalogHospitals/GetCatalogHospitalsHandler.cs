using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogHospitals;

public sealed class GetCatalogHospitalsHandler(ICatalogReader reader)
    : IQueryHandler<GetCatalogHospitalsQuery, Result<IReadOnlyList<CatalogHospitalDto>>>
{
    public async ValueTask<Result<IReadOnlyList<CatalogHospitalDto>>> Handle(
        GetCatalogHospitalsQuery query,
        CancellationToken cancellationToken) =>
        Result.Success(await reader.GetHospitalsAsync(cancellationToken));
}
