using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Queries.GetService;

public sealed class GetServiceHandler(IServiceReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetServiceQuery, Result<ServiceDto>>
{
    public async ValueTask<Result<ServiceDto>> Handle(GetServiceQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        var service = await reader.GetAsync(query.Id, scope, cancellationToken);
        return service is null ? ServiceErrors.NotFound : service;
    }
}
