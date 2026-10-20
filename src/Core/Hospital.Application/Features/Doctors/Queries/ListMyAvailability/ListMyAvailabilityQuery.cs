using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListMyAvailability;

public sealed record ListMyAvailabilityQuery : IQuery<Result<IReadOnlyList<DoctorAvailabilityDto>>>;
