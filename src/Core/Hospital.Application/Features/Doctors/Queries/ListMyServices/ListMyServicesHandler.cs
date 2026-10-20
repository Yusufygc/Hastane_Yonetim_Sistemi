using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListMyServices;

public sealed class ListMyServicesHandler(IDoctorReader reader, ICurrentUser currentUser)
    : IQueryHandler<ListMyServicesQuery, Result<IReadOnlyList<DoctorServiceDto>>>
{
    public async ValueTask<Result<IReadOnlyList<DoctorServiceDto>>> Handle(ListMyServicesQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        return Result.Success(await reader.ListServicesAsync(userId, cancellationToken));
    }
}
