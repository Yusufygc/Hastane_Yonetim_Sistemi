using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Auth.Commands.ChangePassword;
using Hospital.Application.Features.Auth.Commands.Login;
using Hospital.Application.Features.Auth.Commands.Logout;
using Hospital.Application.Features.Auth.Commands.RefreshToken;
using Hospital.Application.Features.Auth.Commands.RegisterPatient;
using Hospital.Application.Features.Auth.Commands.RequestPasswordReset;
using Hospital.Application.Features.Auth.Commands.ResetPassword;
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

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

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

                return result.Match(id => Results.Json(new UserCreatedResponse(id), statusCode: StatusCodes.Status201Created));
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy)
            .Produces<UserCreatedResponse>(StatusCodes.Status201Created);

        // Başarılı girişte kısa ömürlü JWT (accessToken) + yenileme belirteci (refreshToken) döner.
        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new LoginCommand(request.Email, request.Password), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy)
            .Produces<AuthTokens>(StatusCodes.Status200OK);

        // Yenileme belirteci döndürme (rotation): eski belirteç iptal edilir, yeni çift verilir.
        group.MapPost("/refresh", async (RefreshRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RefreshTokenCommand(request.RefreshToken), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy)
            .Produces<AuthTokens>(StatusCodes.Status200OK);

        // Oturumu kapatır: yenileme belirtecini iptal eder. Erişim JWT'si süresi dolana kadar geçerli kalır
        // (kısa ömürlü olduğu için); hesap pasife alınırsa anında geçersiz olur.
        group.MapPost("/logout", async (RefreshRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new LogoutCommand(request.RefreshToken), ct)).ToNoContent())
            .RequireAuthorization(Policies.Authenticated)
            .Produces(StatusCodes.Status204NoContent);

        // Parola değişince güvenlik damgası döner (mevcut erişim belirteçleri düşer) ve yenileme belirteçleri iptal edilir.
        group.MapPost("/change-password", async (ChangePasswordRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct)).ToNoContent())
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy)
            .Produces(StatusCodes.Status204NoContent);

        // "Şifremi unuttum": yanıt hesap var olsun olmasın aynıdır (202); e-posta arka planda outbox ile gider.
        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RequestPasswordResetCommand(request.Email), ct);
                return result.IsSuccess ? Results.Accepted() : result.ToNoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy)
            .Produces(StatusCodes.Status202Accepted);

        // E-postadaki tek kullanımlık belirteçle yeni parola; tüm oturumlar kapanır ve belirteç bir daha çalışmaz.
        group.MapPost("/reset-password", async (ResetPasswordRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ResetPasswordCommand(request.Token, request.NewPassword), ct)).ToNoContent())
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExt.AuthRateLimitPolicy)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", (ICurrentUser user) =>
                Results.Ok(new MeResponse(user.UserId, user.Email, user.Role)))
            .RequireAuthorization(Policies.Authenticated)
            .Produces<MeResponse>(StatusCodes.Status200OK);
    }
}
