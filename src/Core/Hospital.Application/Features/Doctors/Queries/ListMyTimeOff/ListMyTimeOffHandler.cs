using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListMyTimeOff;

public sealed class ListMyTimeOffHandler(IDoctorReader reader, ICurrentUser currentUser)
    : IQueryHandler<ListMyTimeOffQuery, Result<IReadOnlyList<DoctorTimeOffDto>>>
{
    public async ValueTask<Result<IReadOnlyList<DoctorTimeOffDto>>> Handle(ListMyTimeOffQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        return Result.Success(await reader.ListTimeOffsAsync(userId, cancellationToken));
    }
}
