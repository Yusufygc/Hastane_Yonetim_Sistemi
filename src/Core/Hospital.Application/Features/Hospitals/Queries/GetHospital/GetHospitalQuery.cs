using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Queries.GetHospital;

public sealed record GetHospitalQuery(Guid Id) : IQuery<Result<HospitalDetailDto>>;
