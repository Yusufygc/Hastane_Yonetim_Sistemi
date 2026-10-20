using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Patients.Queries.GetMyPatientProfile;

public sealed record GetMyPatientProfileQuery : IQuery<Result<PatientProfileDto>>;
