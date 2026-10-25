using Hospital.Application.Features.Users.Commands.ActivateUser;
using Hospital.Application.Features.Users.Commands.DeactivateUser;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Users;

internal sealed class UserEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.Admin);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new DeactivateUserCommand(id), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ActivateUserCommand(id), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);
    }
}
