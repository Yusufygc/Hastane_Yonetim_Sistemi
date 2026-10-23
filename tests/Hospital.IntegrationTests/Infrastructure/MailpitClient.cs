using System.Text.Json;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>
/// Yerel Mailpit (docker compose) HTTP API istemcisi: gerçek SMTP ile gönderilen e-postaları doğrulamak içindir.
/// Adres <c>MAILPIT_HOST</c> ortam değişkeniyle değiştirilir (container içinden <c>host.docker.internal</c>).
/// </summary>
public sealed class MailpitClient
{
    public const int SmtpPort = 1025;
    public const int ApiPort = 8025;

    private readonly HttpClient _http;

    public MailpitClient()
    {
        Host = Environment.GetEnvironmentVariable("MAILPIT_HOST") ?? "localhost";
        _http = new HttpClient { BaseAddress = new Uri($"http://{Host}:{ApiPort}"), Timeout = TimeSpan.FromSeconds(10) };
    }

    public string Host { get; }

    /// <summary>Mailpit çalışmıyorsa açıklayıcı hata verir (testin zaman aşımına düşmesi yerine).</summary>
    public async Task EnsureAvailableAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/api/v1/info");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Mailpit'e ulaşılamadı ({Host}:{ApiPort}). Entegrasyon testleri için 'docker compose up -d' çalışmalıdır.", ex);
        }
    }

    /// <summary>Alıcıya giden mesajları, en az <paramref name="expected"/> adet olana kadar (zaman aşımına dek) bekleyerek döner.</summary>
    public async Task<IReadOnlyList<MailpitMessage>> WaitForMessagesAsync(string toAddress, int expected = 1, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (true)
        {
            var messages = await SearchAsync(toAddress);
            if (messages.Count >= expected || DateTime.UtcNow >= deadline)
            {
                return messages;
            }

            await Task.Delay(300);
        }
    }

    public async Task<IReadOnlyList<MailpitMessage>> SearchAsync(string toAddress)
    {
        using var response = await _http.GetAsync($"/api/v1/search?query={Uri.EscapeDataString($"to:\"{toAddress}\"")}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var result = new List<MailpitMessage>();
        foreach (var item in document.RootElement.GetProperty("messages").EnumerateArray())
        {
            var id = item.GetProperty("ID").GetString()!;
            using var detail = await _http.GetAsync($"/api/v1/message/{id}");
            detail.EnsureSuccessStatusCode();
            using var detailDocument = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
            var root = detailDocument.RootElement;

            result.Add(new MailpitMessage(
                id,
                root.GetProperty("Subject").GetString()!,
                root.GetProperty("From").GetProperty("Address").GetString()!,
                root.GetProperty("To").EnumerateArray().Select(t => t.GetProperty("Address").GetString()!).ToList(),
                root.GetProperty("Text").GetString()!));
        }

        return result;
    }
}

public sealed record MailpitMessage(string Id, string Subject, string FromAddress, IReadOnlyList<string> To, string Text);
