using Hospital.Application.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hospital.Infrastructure.Features.Notifications;

/// <summary>
/// Outbox işleyici (<see cref="BackgroundService"/>): bekleyen e-postaları sırayla gönderir. Ana akıştan bağımsızdır:
/// SMTP kapalıysa randevu işlemleri etkilenmez, mesajlar üstel geri çekilmeyle yeniden denenir ve SMTP gelince gider.
/// Her mesaj ayrı kapsam ve transaction'da işlenir (<see cref="IEmailOutboxStore"/>, <c>SKIP LOCKED</c>).
/// Loglara alıcı adresi veya içerik yazılmaz; yalnız mesaj kimliği ve sonuç.
/// </summary>
internal sealed class EmailOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    NotificationSettings settings,
    TimeProvider clock,
    ILogger<EmailOutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = settings.Outbox;

        if (!options.Enabled)
        {
            logger.LogInformation("E-posta outbox işleyicisi kapalı (Email:Outbox:Enabled=false); mesajlar birikir.");
            return;
        }

        logger.LogInformation("E-posta outbox işleyicisi başladı (aralık {Interval}).", options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;

            try
            {
                while (processed < options.BatchPerCycle && await ProcessOneAsync(options, stoppingToken))
                {
                    processed++;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Veritabanı geçici olarak yoksa vb.: işleyici ölmez, sonraki turda yeniden dener.
                logger.LogError(ex, "E-posta outbox turu başarısız; sonraki turda yeniden denenecek.");
            }

            // Tur dolduysa (iş birikmiş olabilir) beklemeden devam et; aksi halde yokla.
            if (processed < options.BatchPerCycle)
            {
                try
                {
                    await Task.Delay(options.PollInterval, clock, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Hazır bir mesaj işlendiyse <c>true</c> (sonuç ne olursa olsun).</summary>
    private async Task<bool> ProcessOneAsync(OutboxSettings options, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IEmailOutboxStore>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var result = await store.ProcessNextAsync(sender, options.Retry, clock.GetUtcNow(), cancellationToken);

        switch (result.Outcome)
        {
            case OutboxOutcome.Sent:
                logger.LogInformation("E-posta gönderildi (outbox {MessageId}).", result.MessageId);
                break;
            case OutboxOutcome.RetryScheduled:
                logger.LogWarning("E-posta gönderilemedi, yeniden denenecek (outbox {MessageId}).", result.MessageId);
                break;
            case OutboxOutcome.GaveUp:
                logger.LogError("E-posta gönderilemedi, deneme sınırı aşıldı (outbox {MessageId}).", result.MessageId);
                break;
        }

        return result.Outcome != OutboxOutcome.Idle;
    }
}
