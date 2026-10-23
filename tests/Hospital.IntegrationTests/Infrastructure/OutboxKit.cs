using Hospital.Application.Features.Notifications;
using Hospital.Domain.Entities.Notifications;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>E-posta outbox testleri için yardımcılar (satır okuma, mağaza üzerinden işleme).</summary>
public static class OutboxKit
{
    public static async Task<string> EmailOfAsync(ApiClient client) =>
        (await ApiClient.ReadJsonAsync(await client.GetAsync("/api/auth/me"))).GetProperty("email").GetString()!;

    public static async Task<Guid> IdOfAsync(ApiClient client) =>
        (await ApiClient.ReadJsonAsync(await client.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();

    public static async Task<List<EmailOutboxMessage>> RowsAsync(HospitalApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailOutbox.AsNoTracking().OrderBy(m => m.CreatedAt).ToListAsync();
    }

    public static async Task<List<EmailOutboxMessage>> ToAsync(HospitalApiFactory factory, string address) =>
        (await RowsAsync(factory)).Where(m => m.ToAddress == address).ToList();

    public static async Task<EmailOutboxMessage> RowAsync(HospitalApiFactory factory, Guid id) =>
        (await RowsAsync(factory)).Single(m => m.Id == id);

    /// <summary>
    /// Hazır mesajları <paramref name="sender"/> ile tek tek işler; hazır mesaj kalmayınca durur. <paramref name="now"/> ile
    /// zaman ilerletilerek geri çekilme beklemesi atlanabilir. İşlenen mesaj sonuçlarını döner.
    /// </summary>
    public static async Task<List<OutboxProcessResult>> ProcessAllAsync(
        HospitalApiFactory factory,
        IEmailSender sender,
        DateTimeOffset? now = null,
        RetryPolicy? policy = null)
    {
        var results = new List<OutboxProcessResult>();

        while (true)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IEmailOutboxStore>();

            var result = await store.ProcessNextAsync(sender, policy ?? RetryPolicy.Default, now ?? DateTimeOffset.UtcNow, CancellationToken.None);
            if (result.Outcome == OutboxOutcome.Idle)
            {
                return results;
            }

            results.Add(result);
        }
    }

    /// <summary>Önceki testlerden kalan tüm bekleyen mesajları (geri çekilme dahil) boşaltır; böylece sıra temiz başlar.</summary>
    public static async Task DrainAsync(HospitalApiFactory factory) =>
        await ProcessAllAsync(factory, new FakeEmailSender(), DateTimeOffset.UtcNow.AddDays(1));
}
