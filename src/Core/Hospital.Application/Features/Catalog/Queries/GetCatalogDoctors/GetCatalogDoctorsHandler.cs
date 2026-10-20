using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogDoctors;

public sealed class GetCatalogDoctorsHandler(ICatalogReader reader)
    : IQueryHandler<GetCatalogDoctorsQuery, Result<IReadOnlyList<CatalogDoctorDto>>>
{
    public async ValueTask<Result<IReadOnlyList<CatalogDoctorDto>>> Handle(
        GetCatalogDoctorsQuery query,
        CancellationToken cancellationToken)
    {
        var doctors = await reader.GetDoctorsAsync(query.HospitalId, query.ServiceId, cancellationToken);
        return doctors is null ? CatalogErrors.HospitalNotFound : Result.Success(doctors);
    }
}
