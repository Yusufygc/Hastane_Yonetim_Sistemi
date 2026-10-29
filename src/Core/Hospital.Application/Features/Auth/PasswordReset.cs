using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Auth;

/// <summary>
/// Parola sıfırlama ayarları (<c>PasswordReset</c> ve <c>Frontend</c> bölümleri). <c>FrontendBaseUrl</c> e-postadaki bağlantının
/// kökü (arayüzün adresi); eksik/geçersizse uygulama açılışta hata verir (bozuk bağlantılı e-posta gitmesin).
/// </summary>
public sealed record PasswordResetSettings(TimeSpan TokenLifetime, TimeSpan ResendInterval, string FrontendBaseUrl)
{
    public static PasswordResetSettings Default { get; } = new(
        TokenLifetime: TimeSpan.FromMinutes(30),
        ResendInterval: TimeSpan.FromMinutes(2),
        FrontendBaseUrl: string.Empty);

    public PasswordResetSettings Validated()
    {
        if (!Uri.TryCreate(FrontendBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "Frontend:BaseUrl zorunludur ve mutlak bir http(s) adresi olmalıdır (parola sıfırlama e-postasındaki bağlantının kökü; örn. http://localhost:5173).");
        }

        if (TokenLifetime <= TimeSpan.Zero || ResendInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException("PasswordReset:LifetimeMinutes pozitif, PasswordReset:ResendIntervalSeconds negatif olmayan olmalıdır.");
        }

        return this;
    }

    /// <summary>E-postadaki sıfırlama bağlantısı. Belirteç base64url olduğundan sorgu dizesinde güvenlidir.</summary>
    public string ResetLink(string token) => $"{FrontendBaseUrl.TrimEnd('/')}/sifre-sifirla?token={Uri.EscapeDataString(token)}";
}

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Kullanıcının kullanılmamış ve süresi dolmamış belirteçleri (yenisi istenince eskiler geçersiz kılınır).</summary>
    Task<IReadOnlyList<PasswordResetToken>> GetUsableByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(PasswordResetToken token);
}
