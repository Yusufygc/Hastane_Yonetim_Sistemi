using System.Net.Http.Json;
using System.Text.Json;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>
/// Tarayıcı gibi davranan istemci: cookie'leri saklar, durum değiştiren isteklerden önce
/// güncel CSRF belirtecini alıp başlığa ekler (kimlik değişince belirteç de değişir).
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private sealed record CsrfResponse(string HeaderName, string Token);

    public HttpClient Http => http;

    public Task<HttpResponseMessage> GetAsync(string url) => http.GetAsync(url);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool withCsrf = true) =>
        SendAsync(HttpMethod.Post, url, body, withCsrf);

    public Task<HttpResponseMessage> PutAsync(string url, object? body = null) =>
        SendAsync(HttpMethod.Put, url, body, withCsrf: true);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool withCsrf = true)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (withCsrf)
        {
            var csrf = await http.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
            request.Headers.Add(csrf!.HeaderName, csrf.Token);
        }

        return await http.SendAsync(request);
    }

    public async Task LoginAsync(string email, string password)
    {
        var response = await PostAsync("/api/auth/login", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
    }

    public async Task<TestAccount> RegisterAndLoginAsync()
    {
        var account = TestAccount.New();

        var register = await PostAsync("/api/auth/register", account.RegistrationBody());
        register.EnsureSuccessStatusCode();

        await LoginAsync(account.Email, account.Password);

        return account;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
