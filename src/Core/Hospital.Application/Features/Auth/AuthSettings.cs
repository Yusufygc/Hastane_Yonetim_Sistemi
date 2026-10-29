namespace Hospital.Application.Features.Auth;

/// <summary>Giriş güvenliği ve belirteç ömürleri (Auth ve Jwt yapılandırma bölümlerinden).</summary>
public sealed record AuthSettings(
    int MaxFailedLoginAttempts,
    TimeSpan LockoutDuration,
    TimeSpan AccessTokenLifetime,
    TimeSpan RefreshTokenLifetime)
{
    public static AuthSettings Default { get; } = new(
        MaxFailedLoginAttempts: 5,
        LockoutDuration: TimeSpan.FromMinutes(15),
        AccessTokenLifetime: TimeSpan.FromMinutes(15),
        RefreshTokenLifetime: TimeSpan.FromDays(7));
}

/// <summary>
/// JWT imzalama/doğrulama ayarları (Jwt bölümü). Anahtar en az 32 bayt olmalıdır; üretimde ortam değişkeni
/// veya gizli depo ile verilir, koda/depoya gömülmez.
/// </summary>
public sealed record JwtSettings(string Issuer, string Audience, string SigningKey)
{
    public const int MinSigningKeyBytes = 32;

    /// <summary>Eksik/kısa anahtarı uygulama ayağa kalkarken yakalar (sessizce güvensiz çalışmaz).</summary>
    public JwtSettings Validated()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer ve Jwt:Audience ayarları zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey) || System.Text.Encoding.UTF8.GetByteCount(SigningKey) < MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey en az {MinSigningKeyBytes} bayt olmalıdır (ortam değişkeni veya gizli depo ile verin).");
        }

        return this;
    }
}

/// <summary>Başarılı girişten sonra erişim belirteci üretmek için gereken kullanıcı bilgileri.</summary>
public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string Role,
    string SecurityStamp,
    IReadOnlyDictionary<string, string> ScopeClaims);

/// <summary>İstemciye dönen belirteç çifti. Yenileme belirtecinin düz değeri yalnızca burada görünür.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserInfo User);

public sealed record AuthUserInfo(Guid Id, string Email, string Role);

public static class AuthErrors
{
    public static readonly Wrappers.Error InvalidCredentials =
        Wrappers.Error.Unauthorized("auth.invalid_credentials", "E-posta veya parola hatalı.");

    public static readonly Wrappers.Error InvalidRefreshToken =
        Wrappers.Error.Unauthorized("auth.invalid_refresh_token", "Oturum geçersiz veya süresi dolmuş. Lütfen yeniden giriş yapın.");

    public static readonly Wrappers.Error CurrentPasswordInvalid =
        Wrappers.Error.Validation(
            "auth.current_password_invalid",
            "Mevcut parola hatalı.",
            new Dictionary<string, string[]> { ["currentPassword"] = ["Mevcut parola hatalı."] });

    public static readonly Wrappers.Error PasswordUnchanged =
        Wrappers.Error.Validation(
            "auth.password_unchanged",
            "Yeni parola mevcut parolayla aynı olamaz.",
            new Dictionary<string, string[]> { ["newPassword"] = ["Yeni parola mevcut parolayla aynı olamaz."] });

    public static readonly Wrappers.Error InvalidResetToken =
        Wrappers.Error.Validation(
            "auth.invalid_reset_token",
            "Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş. Lütfen yeni bir bağlantı isteyin.",
            new Dictionary<string, string[]> { ["token"] = ["Bağlantı geçersiz veya süresi dolmuş."] });

    public static readonly Wrappers.Error EmailTaken =
        Wrappers.Error.Conflict("auth.email_taken", "Bu e-posta adresi zaten kayıtlı.");

    public static readonly Wrappers.Error IdentityNumberTaken =
        Wrappers.Error.Conflict("auth.identity_number_taken", "Bu T.C. kimlik numarası zaten kayıtlı.");
}
