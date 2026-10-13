using Hospital.Application.Features.Organizations.Commands.UpdateOrganization;
using Hospital.Application.Features.Organizations.Queries.GetOrganization;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Organizations;

public sealed record UpdateOrganizationRequest(string Name);

internal sealed class OrganizationEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organization")
            .WithTags("Organization")
            .RequireAuthorization(Policies.SuperAdmin);

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetOrganizationQuery(), ct)).Match(Results.Ok));

        group.MapPut("/", async (UpdateOrganizationRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(new UpdateOrganizationCommand(request.Name), ct)).ToNoContent());
    }
}
