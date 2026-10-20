using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListMyAvailability;

public sealed class ListMyAvailabilityHandler(IDoctorReader reader, ICurrentUser currentUser)
    : IQueryHandler<ListMyAvailabilityQuery, Result<IReadOnlyList<DoctorAvailabilityDto>>>
{
    public async ValueTask<Result<IReadOnlyList<DoctorAvailabilityDto>>> Handle(
        ListMyAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        return Result.Success(await reader.ListAvailabilitiesAsync(userId, cancellationToken));
    }
}
