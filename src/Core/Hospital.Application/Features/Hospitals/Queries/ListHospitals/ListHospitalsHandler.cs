using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Queries.ListHospitals;

public sealed class ListHospitalsHandler(IHospitalReader reader, ICurrentUser currentUser)
    : IQueryHandler<ListHospitalsQuery, Result<IReadOnlyList<HospitalDto>>>
{
    public async ValueTask<Result<IReadOnlyList<HospitalDto>>> Handle(ListHospitalsQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        return Result.Success(await reader.ListAsync(scope, cancellationToken));
    }
}
