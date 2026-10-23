using Hospital.Domain.Entities.Notifications;

namespace Hospital.Application.Features.Notifications;

/// <summary>Outbox'a e-posta satırı ekler. Satır, çağıranla aynı DbContext'e eklenir (SaveChanges ile atomik).</summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Mesajı ekler. Aynı <see cref="EmailOutboxMessage.DedupKey"/> zaten varsa (aynı olay + alıcı) eklemez ve
    /// <c>false</c> döner: aynı olay için tek e-posta.
    /// </summary>
    Task<bool> EnqueueAsync(EmailOutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>Arka plan işleyicinin outbox'tan güvenli (çoklu örnekte çift göndermeyen) okuma/yazma arayüzü.</summary>
public interface IEmailOutboxStore
{
    /// <summary>
    /// Gönderimi hazır sıradaki bekleyen mesajı <c>FOR UPDATE SKIP LOCKED</c> ile kilitler, <paramref name="sender"/> ile
    /// göndermeyi dener ve sonucu (gönderildi / yeniden denenecek / vazgeçildi) <b>aynı transaction'da</b> yazar.
    /// Başka bir işleyici kilitli satırı atlar; böylece iki örnek aynı mesajı göndermez.
    /// </summary>
    Task<OutboxProcessResult> ProcessNextAsync(
        IEmailSender sender,
        RetryPolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public enum OutboxOutcome
{
    /// <summary>Gönderilecek hazır mesaj yok.</summary>
    Idle,

    Sent,

    /// <summary>Gönderim başarısız; üstel geri çekilmeyle yeniden denenecek.</summary>
    RetryScheduled,

    /// <summary>Deneme sınırı aşıldı; mesaj <c>FAILED</c> oldu.</summary>
    GaveUp,
}

public sealed record OutboxProcessResult(OutboxOutcome Outcome, Guid? MessageId = null);

public sealed record EmailMessage(string ToAddress, string? ToName, string Subject, string Body);

/// <summary>SMTP gönderimi (Infrastructure: MailKit). Başarısızlıkta istisna fırlatır; çağıran yeniden dener.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>E-posta içeriği için alıcı bilgilerini okur (okuma tarafı; olay dağıtımı sırasında aynı DbContext'ten).</summary>
public interface INotificationReader
{
    Task<AppointmentRecipients?> GetAppointmentRecipientsAsync(
        Guid patientId,
        Guid doctorId,
        Guid hospitalId,
        CancellationToken cancellationToken);
}

/// <param name="TimeZoneId">Şubenin IANA saat dilimi; e-postadaki tarih/saat bu dilimde yazılır.</param>
public sealed record AppointmentRecipients(
    string PatientEmail,
    string PatientName,
    string DoctorEmail,
    string DoctorName,
    string HospitalName,
    string TimeZoneId);

public enum SmtpSecurity
{
    /// <summary>Şifresiz (yerel Mailpit).</summary>
    None,

    /// <summary>STARTTLS zorunlu (genellikle 587).</summary>
    StartTls,

    /// <summary>Doğrudan TLS (genellikle 465).</summary>
    Ssl,
}

/// <summary>SMTP bağlantısı (<c>Email:Smtp</c>). Parola yapılandırma dosyasına değil ortam değişkeni/gizli depoya konur.</summary>
public sealed record SmtpSettings(
    string Host,
    int Port,
    SmtpSecurity Security,
    string? UserName,
    string? Password,
    string FromAddress,
    string FromName)
{
    public static SmtpSettings Default { get; } = new(
        Host: "localhost",
        Port: 1025,
        Security: SmtpSecurity.None,
        UserName: null,
        Password: null,
        FromAddress: "noreply@hastane.local",
        FromName: "Hastane Randevu Sistemi");
}

/// <summary>Outbox işleyici ayarları (<c>Email:Outbox</c>).</summary>
/// <param name="Enabled">Kapalıysa arka plan işleyici başlamaz (mesajlar birikir, uygulama bozulmaz).</param>
/// <param name="PollInterval">Boşken sonraki yoklama aralığı.</param>
/// <param name="BatchPerCycle">Bir turda en fazla kaç mesaj işlenir.</param>
public sealed record OutboxSettings(bool Enabled, TimeSpan PollInterval, int BatchPerCycle, RetryPolicy Retry)
{
    public static OutboxSettings Default { get; } = new(true, TimeSpan.FromSeconds(5), 20, RetryPolicy.Default);
}

public sealed record NotificationSettings(SmtpSettings Smtp, OutboxSettings Outbox)
{
    public static NotificationSettings Default { get; } = new(SmtpSettings.Default, OutboxSettings.Default);
}
