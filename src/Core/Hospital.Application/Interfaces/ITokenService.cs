using Hospital.Application.Features.Auth;

namespace Hospital.Application.Interfaces;

/// <summary>Kısa ömürlü JWT erişim belirteci üretir (imzalama altyapısı Infrastructure'dadır).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AuthenticatedUser user, DateTimeOffset now);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
