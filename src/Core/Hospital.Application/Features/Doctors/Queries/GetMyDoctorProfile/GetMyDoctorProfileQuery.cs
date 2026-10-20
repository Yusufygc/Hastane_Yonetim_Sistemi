using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.GetMyDoctorProfile;

public sealed record GetMyDoctorProfileQuery : IQuery<Result<DoctorDetailDto>>;
