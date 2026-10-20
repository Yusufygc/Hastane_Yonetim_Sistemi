using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogDoctors;

/// <param name="ServiceId">Verilirse yalnızca bu hizmeti (yönetici onaylı) sunan doktorlar.</param>
public sealed record GetCatalogDoctorsQuery(Guid HospitalId, Guid? ServiceId) : IQuery<Result<IReadOnlyList<CatalogDoctorDto>>>;
