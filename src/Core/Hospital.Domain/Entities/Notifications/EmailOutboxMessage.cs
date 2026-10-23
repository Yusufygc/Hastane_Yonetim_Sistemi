using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Notifications;

public enum EmailStatus
{
    /// <summary>Gönderilmeyi bekliyor (veya yeniden denenecek).</summary>
    Pending = 1,

    Sent = 2,

    /// <summary>Deneme sınırı aşıldı; otomatik yeniden denenmez.</summary>
    Failed = 3,
}

public static class EmailStatusNames
{
    public static string From(EmailStatus status) => status.ToString().ToUpperInvariant();

    public static EmailStatus Parse(string value) => Enum.Parse<EmailStatus>(value, ignoreCase: true);
}

/// <summary>Yeniden deneme politikası: üstel geri çekilme (<c>BaseDelay · 2^(deneme-1)</c>, <c>MaxDelay</c> ile sınırlı).</summary>
public sealed record RetryPolicy(int MaxAttempts, TimeSpan BaseDelay, TimeSpan MaxDelay)
{
    public static RetryPolicy Default { get; } = new(5, TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));

    public TimeSpan DelayAfter(int attempt)
    {
        var factor = Math.Pow(2, Math.Max(attempt - 1, 0));
        var seconds = Math.Min(BaseDelay.TotalSeconds * factor, MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}

/// <summary>
/// Gönderilecek e-posta kaydı (EMAIL_OUTBOX, Transactional Outbox). Randevu ile aynı transaction'da yazılır; arka plan
/// işleyici SMTP ile gönderir. <see cref="DedupKey"/> (olay kimliği + alıcı) aynı olay için tek e-posta garantisi verir.
/// </summary>
public sealed class EmailOutboxMessage : Entity
{
    public const int DedupKeyMaxLength = 200;
    public const int AddressMaxLength = 254;
    public const int NameMaxLength = 200;
    public const int SubjectMaxLength = 200;
    public const int TemplateKeyMaxLength = 100;
    public const int ErrorMaxLength = 1000;

    /// <summary>EF Core materialization için.</summary>
    private EmailOutboxMessage()
    {
        DedupKey = null!;
        ToAddress = null!;
        Subject = null!;
        Body = null!;
        TemplateKey = null!;
    }

    private EmailOutboxMessage(
        Guid id,
        string dedupKey,
        string toAddress,
        string? toName,
        string subject,
        string body,
        string templateKey,
        DateTimeOffset now)
        : base(id)
    {
        DedupKey = dedupKey;
        ToAddress = toAddress;
        ToName = toName;
        Subject = subject;
        Body = body;
        TemplateKey = templateKey;
        Status = EmailStatus.Pending;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public string DedupKey { get; private set; }

    public string ToAddress { get; private set; }

    public string? ToName { get; private set; }

    public string Subject { get; private set; }

    public string Body { get; private set; }

    /// <summary>Şablon anahtarı (ör. <c>appointment.requested.patient</c>); izleme ve hata ayıklama içindir.</summary>
    public string TemplateKey { get; private set; }

    public EmailStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Bu andan önce gönderim denenmez (geri çekilme).</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public static EmailOutboxMessage Create(
        string dedupKey,
        string toAddress,
        string? toName,
        string subject,
        string body,
        string templateKey,
        DateTimeOffset now)
    {
        var address = Guard.NotBlank(toAddress, "Alıcı e-postası", AddressMaxLength);
        if (!address.Contains('@'))
        {
            throw new DomainException("Alıcı e-posta adresi geçersiz.");
        }

        return new EmailOutboxMessage(
            Guid.CreateVersion7(),
            Guard.NotBlank(dedupKey, "Tekrar önleme anahtarı", DedupKeyMaxLength),
            address,
            Guard.OptionalText(toName, "Alıcı adı", NameMaxLength),
            Guard.NotBlank(subject, "Konu", SubjectMaxLength),
            Guard.NotBlank(body, "İçerik", int.MaxValue),
            Guard.NotBlank(templateKey, "Şablon anahtarı", TemplateKeyMaxLength),
            now.ToUniversalTime());
    }

    /// <summary>Başarılı gönderim. Yalnız bekleyen kayıt gönderilmiş sayılır.</summary>
    public void MarkSent(DateTimeOffset now)
    {
        if (Status != EmailStatus.Pending)
        {
            throw new DomainException("Yalnızca bekleyen e-posta gönderilmiş olarak işaretlenebilir.");
        }

        AttemptCount++;
        Status = EmailStatus.Sent;
        SentAt = now.ToUniversalTime();
        LastError = null;
    }

    /// <summary>
    /// Başarısız deneme: sayaç artar; sınır aşılırsa <see cref="EmailStatus.Failed"/>, aksi halde üstel geri çekilmeyle
    /// yeniden denenmek üzere bekleyen kalır. Hata iletisi kısaltılarak saklanır.
    /// </summary>
    public void RegisterFailure(string error, DateTimeOffset now, RetryPolicy policy)
    {
        if (Status != EmailStatus.Pending)
        {
            throw new DomainException("Yalnızca bekleyen e-posta için başarısız deneme kaydedilebilir.");
        }

        AttemptCount++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;

        if (AttemptCount >= policy.MaxAttempts)
        {
            Status = EmailStatus.Failed;
            return;
        }

        NextAttemptAt = now.ToUniversalTime() + policy.DelayAfter(AttemptCount);
    }
}
