using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Queries.GetHospital;

public sealed class GetHospitalHandler(IHospitalReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetHospitalQuery, Result<HospitalDetailDto>>
{
    public async ValueTask<Result<HospitalDetailDto>> Handle(GetHospitalQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        // Okuyucu kapsam dışı şubeyi hiç döndürmez; yoksa "bulunamadı".
        var detail = await reader.GetDetailAsync(query.Id, scope, cancellationToken);
        return detail is null ? HospitalErrors.NotFound : detail;
    }
}
