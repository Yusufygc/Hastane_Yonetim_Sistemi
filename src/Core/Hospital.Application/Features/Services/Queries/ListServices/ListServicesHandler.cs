using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Queries.ListServices;

public sealed class ListServicesHandler(
    IHospitalRepository hospitals,
    IServiceReader reader,
    ICurrentUser currentUser) : IQueryHandler<ListServicesQuery, Result<IReadOnlyList<ServiceDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceDto>>> Handle(ListServicesQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        if (query.HospitalId is { } hospitalId)
        {
            var hospital = await hospitals.GetByIdAsync(hospitalId, includeWorkingHours: false, cancellationToken);
            if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
            {
                return error;
            }
        }

        return Result.Success(await reader.ListAsync(scope, query.HospitalId, cancellationToken));
    }
}
