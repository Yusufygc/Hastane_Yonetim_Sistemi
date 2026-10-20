using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.GetMyDoctorProfile;

public sealed class GetMyDoctorProfileHandler(IDoctorReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorProfileQuery, Result<DoctorDetailDto>>
{
    public async ValueTask<Result<DoctorDetailDto>> Handle(GetMyDoctorProfileQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        var doctor = await reader.GetOwnAsync(userId, cancellationToken);
        return doctor is null ? DoctorErrors.NotFound : doctor;
    }
}
