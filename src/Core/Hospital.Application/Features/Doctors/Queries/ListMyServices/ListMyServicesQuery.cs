using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListMyServices;

/// <summary>Doktorun kendi hizmet talepleri/atamaları (tüm durumlar).</summary>
public sealed record ListMyServicesQuery : IQuery<Result<IReadOnlyList<DoctorServiceDto>>>;
