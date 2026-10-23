using Hospital.Application.Features.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Hospital.Infrastructure.Features.Notifications;

/// <summary>
/// SMTP gönderimi (MailKit). Her gönderim kısa ömürlü bir bağlantı açar (düşük hacim; bağlantı havuzu gereksiz karmaşa).
/// Hata istisna olarak yükselir; yeniden deneme ve geri çekilme outbox işleyicisindedir. Parola yapılandırma dosyasından
/// değil ortam değişkeninden gelir ve hiçbir yerde loglanmaz.
/// </summary>
internal sealed class MailKitEmailSender(NotificationSettings settings) : IEmailSender
{
    private const int TimeoutMilliseconds = 15_000;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = settings.Smtp;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToAddress, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient { Timeout = TimeoutMilliseconds };

        await client.ConnectAsync(smtp.Host, smtp.Port, ToSocketOptions(smtp.Security), cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.Ssl => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        _ => SecureSocketOptions.None,
    };
}
