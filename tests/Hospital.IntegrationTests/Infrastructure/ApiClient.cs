using System.Net.Http.Json;

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

    public async Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool withCsrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);

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

    public async Task<TestAccount> RegisterAndLoginAsync()
    {
        var account = TestAccount.New();

        var register = await PostAsync("/api/auth/register", account.RegistrationBody());
        register.EnsureSuccessStatusCode();

        var login = await PostAsync("/api/auth/login", account.LoginBody());
        login.EnsureSuccessStatusCode();

        return account;
    }
}
