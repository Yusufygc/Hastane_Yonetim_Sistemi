using Hospital.Application.Features.Auth.Commands.Login;
using Hospital.Application.Features.Auth.Commands.Logout;
using Hospital.Application.Features.Auth.Commands.RefreshToken;
using Hospital.Application.Features.Auth.Commands.RegisterPatient;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Hospital.WebAPI.Extensions;
using Mediator;

namespace Hospital.WebAPI.Features.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string IdentityNumber,
    DateOnly BirthDate,
    string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

internal sealed class AuthEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

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
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy);

        // Başarılı girişte kısa ömürlü JWT (accessToken) + yenileme belirteci (refreshToken) döner.
        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new LoginCommand(request.Email, request.Password), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy);

        // Yenileme belirteci döndürme (rotation): eski belirteç iptal edilir, yeni çift verilir.
        group.MapPost("/refresh", async (RefreshRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RefreshTokenCommand(request.RefreshToken), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy);

        // Oturumu kapatır: yenileme belirtecini iptal eder. Erişim JWT'si süresi dolana kadar geçerli kalır
        // (kısa ömürlü olduğu için); hesap pasife alınırsa anında geçersiz olur.
        group.MapPost("/logout", async (RefreshRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new LogoutCommand(request.RefreshToken), ct)).ToNoContent())
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/me", (ICurrentUser user) =>
                Results.Ok(new { id = user.UserId, email = user.Email, role = user.Role }))
            .RequireAuthorization(Policies.Authenticated);
    }
}
