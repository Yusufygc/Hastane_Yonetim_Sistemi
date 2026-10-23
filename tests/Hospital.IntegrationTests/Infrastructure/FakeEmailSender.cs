using System.Collections.Concurrent;
using Hospital.Application.Features.Notifications;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>
/// Test SMTP göndericisi: gönderilen mesajları kaydeder; <see cref="FailWith"/> doluysa o istisnayı fırlatır
/// ("SMTP kapalı" senaryosu); <see cref="Delay"/> ile gönderim yavaşlatılabilir (eşzamanlılık testleri).
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

    /// <summary>Doluysa her gönderim bu istisnayla başarısız olur.</summary>
    public Exception? FailWith { get; set; }

    public TimeSpan Delay { get; set; }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (FailWith is { } failure)
        {
            throw failure;
        }

        _sent.Enqueue(message);
    }

    public void Reset()
    {
        FailWith = null;
        Delay = TimeSpan.Zero;
        _sent.Clear();
    }
}
