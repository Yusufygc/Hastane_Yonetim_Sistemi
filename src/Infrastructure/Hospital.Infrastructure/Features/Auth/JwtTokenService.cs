using System.Security.Claims;
using System.Text;
using Hospital.Application.Features.Auth;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hospital.Infrastructure.Features.Auth;

/// <summary>HS256 imzalı, kısa ömürlü JWT erişim belirteci üretir.</summary>
internal sealed class JwtTokenService(JwtSettings jwt, AuthSettings auth) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(AuthenticatedUser user, DateTimeOffset now)
    {
        var expires = now + auth.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.Role, user.Role),
            new(ClaimNames.SecurityStamp, user.SecurityStamp),
            new(ClaimNames.TokenId, Guid.NewGuid().ToString("N")),
        };

        claims.AddRange(user.ScopeClaims.Select(pair => new Claim(pair.Key, pair.Value)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}
