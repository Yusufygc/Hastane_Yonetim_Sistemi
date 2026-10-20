using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Patients.Queries.GetMyPatientProfile;

public sealed class GetMyPatientProfileHandler(IUserRepository users, ICurrentUser currentUser)
    : IQueryHandler<GetMyPatientProfileQuery, Result<PatientProfileDto>>
{
    public async ValueTask<Result<PatientProfileDto>> Handle(GetMyPatientProfileQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        return await users.GetByIdAsync(userId, cancellationToken) is Patient patient
            ? PatientProfileDto.From(patient)
            : PatientErrors.NotFound;
    }
}
