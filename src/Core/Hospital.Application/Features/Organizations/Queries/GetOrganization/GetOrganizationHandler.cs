using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Organizations.Queries.GetOrganization;

public sealed class GetOrganizationHandler(IOrganizationRepository organizations, ICurrentUser currentUser)
    : IQueryHandler<GetOrganizationQuery, Result<OrganizationDto>>
{
    public async ValueTask<Result<OrganizationDto>> Handle(GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is not { IsSuperAdmin: true })
        {
            return OrganizationErrors.Forbidden;
        }

        var organization = await organizations.GetByIdAsync(scope.OrganizationId, cancellationToken);
        return organization is null
            ? OrganizationErrors.NotFound
            : new OrganizationDto(organization.Id, organization.Name, organization.IsActive);
    }
}
