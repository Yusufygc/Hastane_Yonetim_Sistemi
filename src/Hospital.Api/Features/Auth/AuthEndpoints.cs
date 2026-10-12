using System.Security.Claims;
using Hospital.Api.Common;
using Hospital.Application.Common.Abstractions;
using Hospital.Application.Common.Security;
using Hospital.Application.Features.Auth.Commands.Login;
using Hospital.Application.Features.Auth.Commands.RegisterPatient;
using Mediator;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Hospital.Api.Features.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string IdentityNumber,
    DateOnly BirthDate,
    string? Phone);

public sealed record LoginRequest(string Email, string Password);

internal sealed class AuthEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Anonim istemci de durum değiştiren istekten önce belirteç alabilmelidir.
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Ok(new { headerName = AuthenticationExtensions.CsrfHeaderName, token = tokens.RequestToken });
            })
            .AllowAnonymous();

        group.MapPost("/register", async (RegisterRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new RegisterPatientCommand(
                        request.Email,
                        request.Password,
                        request.FirstName,
                        request.LastName,
                        request.IdentityNumber,
                        request.BirthDate,
                        request.Phone),
                    ct);

                return result.Match(id => Results.Json(new { userId = id }, statusCode: StatusCodes.Status201Created));
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExtensions.AuthRateLimitPolicy);

        group.MapPost("/login", async (LoginRequest request, HttpContext context, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new LoginCommand(request.Email, request.Password), ct);
                if (result.IsFailure)
                {
                    return result.ToProblem();
                }

                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    AuthenticationExtensions.CreatePrincipal(result.Value));

                return Results.Ok(new { result.Value.Id, result.Value.Email, result.Value.Role });
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExtensions.AuthRateLimitPolicy);

        group.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/me", (ICurrentUser user) =>
                Results.Ok(new { id = user.UserId, email = user.Email, role = user.Role }))
            .RequireAuthorization(Policies.Authenticated);
    }
}
