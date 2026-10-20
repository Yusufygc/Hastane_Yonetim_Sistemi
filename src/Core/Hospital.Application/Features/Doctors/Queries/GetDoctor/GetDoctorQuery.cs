using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.GetDoctor;

public sealed record GetDoctorQuery(Guid Id) : IQuery<Result<DoctorDetailDto>>;
