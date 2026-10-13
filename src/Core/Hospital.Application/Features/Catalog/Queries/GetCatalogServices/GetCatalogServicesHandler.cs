using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogServices;

public sealed class GetCatalogServicesHandler(ICatalogReader reader)
    : IQueryHandler<GetCatalogServicesQuery, Result<IReadOnlyList<CatalogServiceDto>>>
{
    public async ValueTask<Result<IReadOnlyList<CatalogServiceDto>>> Handle(
        GetCatalogServicesQuery query,
        CancellationToken cancellationToken)
    {
        var services = await reader.GetServicesAsync(query.HospitalId, cancellationToken);
        return services is null ? CatalogErrors.HospitalNotFound : Result.Success(services);
    }
}
