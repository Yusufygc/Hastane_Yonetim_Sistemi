using Hospital.Application.Features.Appointments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hospital.Infrastructure.Features.Appointments;

/// <summary>
/// Arka plan işi: doktorun karar vermediği bekleyen randevu taleplerini periyodik olarak otomatik düşürür
/// (<see cref="PendingAppointmentExpirer"/>). Ana akıştan bağımsızdır; hata işleyiciyi durdurmaz. Her randevu ayrı kapsamdadır.
/// </summary>
internal sealed class PendingAppointmentExpiryProcessor(
    IServiceScopeFactory scopeFactory,
    PendingExpirySettings settings,
    TimeProvider clock,
    ILogger<PendingAppointmentExpiryProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Bekleyen randevu zaman aşımı işleyicisi kapalı (Appointment:PendingExpiry:Enabled=false).");
            return;
        }

        logger.LogInformation(
            "Bekleyen randevu zaman aşımı işleyicisi başladı (süre {After}, aralık {Interval}).", settings.After, settings.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var expired = await RunOnceAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("{Count} bekleyen randevu talebi zaman aşımına uğradı ve reddedildi.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Bekleyen randevu zaman aşımı turu başarısız; sonraki turda yeniden denenecek.");
            }

            try
            {
                await Task.Delay(settings.PollInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> dueIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            dueIds = await scope.ServiceProvider.GetRequiredService<PendingAppointmentExpirer>().FindDueAsync(cancellationToken);
        }

        var expired = 0;
        foreach (var id in dueIds)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<PendingAppointmentExpirer>().ExpireAsync(id, cancellationToken))
            {
                expired++;
            }
        }

        return expired;
    }
}
