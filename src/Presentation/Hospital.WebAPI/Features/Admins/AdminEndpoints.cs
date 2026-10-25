using Hospital.Application.Features.Admins;
using Hospital.Application.Features.Admins.Commands.CreateAdmin;
using Hospital.Application.Features.Admins.Queries.ListAdmins;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Admins;

/// <param name="AccessLevel">SUPER_ADMIN veya BRANCH_MANAGER.</param>
public sealed record CreateAdminRequest(
    string AccessLevel,
    Guid? HospitalId,
    string Email,
    string Password,
    string FirstName,
    string LastName);

internal sealed class AdminEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admins").WithTags("Admins");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListAdminsQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Policies.Admin)
            .Produces<IReadOnlyList<AdminDto>>(StatusCodes.Status200OK);

        // Yönetici oluşturmak organizasyon kapsamı ister.
        group.MapPost("/", async (CreateAdminRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new CreateAdminCommand(
                        request.AccessLevel,
                        request.HospitalId,
                        request.Email,
                        request.Password,
                        request.FirstName,
                        request.LastName),
                    ct);

                return result.Match(id => Results.Created($"/api/admins/{id}", new CreatedResponse(id)));
            })
            .RequireAuthorization(Policies.SuperAdmin)
            .Produces<CreatedResponse>(StatusCodes.Status201Created);
    }
}
