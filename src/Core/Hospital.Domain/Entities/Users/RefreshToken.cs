using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

/// <summary>
/// Yenileme belirteci kaydı. Düz metin değer saklanmaz; yalnızca SHA-256 özeti tutulur.
/// Her yenilemede belirteç döndürülür (rotation): eski belirteç iptal edilir ve yerine geçen kaydedilir.
/// İptal edilmiş bir belirtecin tekrar sunulması çalınma işareti sayılır (kullanıcının tüm belirteçleri iptal edilir).
/// </summary>
public sealed class RefreshToken : Entity
{
    /// <summary>EF Core materialization için.</summary>
    private RefreshToken()
    {
        TokenHash = null!;
    }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Yenileme sırasında bu belirtecin yerine geçen belirteç.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("Yenileme belirteci ömrü pozitif olmalıdır.");
        }

        return new RefreshToken(
            Guid.CreateVersion7(),
            Guard.NotEmpty(userId, "Kullanıcı"),
            Guard.NotBlank(tokenHash, "Belirteç özeti", 128),
            now,
            now + lifetime);
    }

    /// <summary>İdempotent: zaten iptal edilmişse ilk iptal zamanı korunur.</summary>
    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        RevokedAt ??= now;
        ReplacedByTokenId ??= replacedByTokenId;
    }
}
