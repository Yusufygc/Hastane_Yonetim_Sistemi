using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Auth;

/// <summary>
/// Login ve Refresh komutlarının ortak parçası: kısa ömürlü erişim JWT'si + yenileme belirteci çifti üretir.
/// Yeni yenileme belirteci kaydını (özetiyle) repository'ye ekler; SaveChanges çağıran handler'dadır.
/// </summary>
public sealed class AuthTokenIssuer(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    AuthSettings settings)
{
    public IssuedTokens Issue(User user, DateTimeOffset now)
    {
        var authenticated = new AuthenticatedUser(
            user.Id,
            user.Email,
            RoleNames.From(user.Role),
            user.SecurityStamp,
            user.ScopeClaims);

        var access = tokenService.CreateAccessToken(authenticated, now);

        var refreshValue = RefreshTokenSecret.Generate();
        var refresh = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash(refreshValue), now, settings.RefreshTokenLifetime);
        refreshTokens.Add(refresh);

        var tokens = new AuthTokens(
            access.Token,
            access.ExpiresAt,
            refreshValue,
            refresh.ExpiresAt,
            new AuthUserInfo(user.Id, user.Email, authenticated.Role));

        return new IssuedTokens(tokens, refresh);
    }
}

/// <summary>İstemciye dönen belirteçler + yeni yenileme belirteci kaydı (rotation için yerine geçen kimliği taşır).</summary>
public sealed record IssuedTokens(AuthTokens Tokens, RefreshToken RefreshTokenRecord);
