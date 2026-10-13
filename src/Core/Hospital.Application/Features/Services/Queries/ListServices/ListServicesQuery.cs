using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Queries.ListServices;

/// <param name="HospitalId">Verilirse yalnızca o şubenin hizmetleri; verilmezse yönetici kapsamındaki tümü.</param>
public sealed record ListServicesQuery(Guid? HospitalId) : IQuery<Result<IReadOnlyList<ServiceDto>>>;
