using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Queries.ListHospitals;

/// <summary>Yöneticinin kapsamındaki şubeler (şube yöneticisi yalnızca kendi şubesini görür).</summary>
public sealed record ListHospitalsQuery : IQuery<Result<IReadOnlyList<HospitalDto>>>;
