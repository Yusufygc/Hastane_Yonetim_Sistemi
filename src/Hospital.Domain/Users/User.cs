using Hospital.Domain.Common;

namespace Hospital.Domain.Users;

/// <summary>
/// Tüm kullanıcı türlerinin soyut tabanı (kalıtım). Veritabanında Table Per Type:
/// <c>user_account</c> taban tablo, alt tipler aynı birincil anahtarı paylaşır.
/// </summary>
public abstract class User : Entity
{
    private static readonly IReadOnlyDictionary<string, string> NoScopeClaims =
        new Dictionary<string, string>();

    /// <summary>EF Core materialization için.</summary>
    protected User()
    {
        Email = null!;
        PasswordHash = null!;
        SecurityStamp = null!;
    }

    protected User(Guid id, string email, string passwordHash, UserRole role, DateTimeOffset now)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("E-posta boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Parola özeti boş olamaz.");
        }

        Email = NormalizeEmail(email);
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        SecurityStamp = NewStamp();
        CreatedAt = now;
    }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Değiştiğinde mevcut oturumlar (cookie) geçersiz olur.</summary>
    public string SecurityStamp { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Çok biçimlilik: her tür kendi görünen adını üretir.</summary>
    public abstract string FullName { get; }

    /// <summary>Çok biçimlilik: kapsam claim'leri (şube/organizasyon) türe göre değişir.</summary>
    public virtual IReadOnlyDictionary<string, string> ScopeClaims => NoScopeClaims;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    public void RecordFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;

        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEnd = now + lockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
        LastLoginAt = now;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Parola özeti boş olamaz.");
        }

        PasswordHash = passwordHash;
        RotateSecurityStamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        RotateSecurityStamp();
    }

    public void Activate() => IsActive = true;

    public void RotateSecurityStamp() => SecurityStamp = NewStamp();

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
