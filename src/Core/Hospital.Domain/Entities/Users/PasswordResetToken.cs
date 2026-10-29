using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

/// <summary>
/// Parola sıfırlama belirteci (kısa ömürlü, tek kullanımlık). Düz değer yalnız kullanıcıya e-postayla gider; veritabanında
/// yalnızca SHA-256 özeti durur (yenileme belirteciyle aynı yaklaşım). Kullanılınca veya yenisi istenince geçersiz olur.
/// </summary>
public sealed class PasswordResetToken : Entity
{
    /// <summary>EF Core materialization için.</summary>
    private PasswordResetToken()
    {
        TokenHash = null!;
    }

    private PasswordResetToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
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

    /// <summary>Kullanıldığında veya başka bir istek nedeniyle geçersiz kılındığında dolar.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public static PasswordResetToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("Parola sıfırlama belirteci ömrü pozitif olmalıdır.");
        }

        return new PasswordResetToken(
            Guid.CreateVersion7(),
            Guard.NotEmpty(userId, "Kullanıcı"),
            Guard.NotBlank(tokenHash, "Belirteç özeti", 128),
            now,
            now + lifetime);
    }

    /// <summary>İdempotent: ilk kullanım/geçersiz kılma zamanı korunur.</summary>
    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;
}
