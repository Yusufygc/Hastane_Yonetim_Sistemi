using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListMyTimeOff;

public sealed record ListMyTimeOffQuery : IQuery<Result<IReadOnlyList<DoctorTimeOffDto>>>;
