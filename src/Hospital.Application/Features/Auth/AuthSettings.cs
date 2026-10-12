namespace Hospital.Application.Features.Auth;

/// <summary>Giriş güvenliği ayarları (Auth yapılandırma bölümü).</summary>
public sealed record AuthSettings(int MaxFailedLoginAttempts, TimeSpan LockoutDuration)
{
    public static AuthSettings Default { get; } = new(5, TimeSpan.FromMinutes(15));
}

/// <summary>Başarılı girişten sonra cookie bileti oluşturmak için gereken bilgiler.</summary>
public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string Role,
    string SecurityStamp,
    IReadOnlyDictionary<string, string> ScopeClaims);

public static class AuthErrors
{
    public static readonly Common.Error InvalidCredentials =
        Common.Error.Unauthorized("auth.invalid_credentials", "E-posta veya parola hatalı.");

    public static readonly Common.Error EmailTaken =
        Common.Error.Conflict("auth.email_taken", "Bu e-posta adresi zaten kayıtlı.");

    public static readonly Common.Error IdentityNumberTaken =
        Common.Error.Conflict("auth.identity_number_taken", "Bu T.C. kimlik numarası zaten kayıtlı.");
}
