using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Testlerde kullanılan JWT ayarları (yalnız test değerleri) ve sahte/bozuk belirteç üretimi.</summary>
public static class TestJwt
{
    public const string Issuer = "HastaneSistemi";
    public const string Audience = "HastaneSistemi.Client";
    public const string SigningKey = "test-only-signing-key-0123456789-abcdefghijklmnop";

    /// <summary>Belirtilen parametrelerle (varsayılan: geçerli ayarlar) HS256 imzalı belirteç üretir.</summary>
    public static string Create(
        IEnumerable<Claim> claims,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static IEnumerable<Claim> UserClaims(Guid userId, string role, string securityStamp, string email = "x@test.local") =>
    [
        new Claim("sub", userId.ToString()),
        new Claim("email", email),
        new Claim("role", role),
        new Claim("security_stamp", securityStamp),
    ];

    /// <summary>İmza bölümünü bozar (içerik aynı, imza geçersiz).</summary>
    public static string Tamper(string token)
    {
        var parts = token.Split('.');
        var signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        return $"{parts[0]}.{parts[1]}.{new string(signature)}";
    }
}
