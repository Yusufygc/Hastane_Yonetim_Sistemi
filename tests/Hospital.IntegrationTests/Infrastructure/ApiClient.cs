using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospital.IntegrationTests.Infrastructure;

public sealed record TokenPair(string AccessToken, string RefreshToken);

/// <summary>
/// Bearer (JWT) kullanan istemci: giriş yanıtındaki erişim belirtecini sonraki isteklerde Authorization başlığına koyar.
/// Çerez veya CSRF belirteci yoktur.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    public HttpClient Http => http;

    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public Task<HttpResponseMessage> GetAsync(string url) => http.GetAsync(url);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) =>
        SendAsync(HttpMethod.Post, url, body);

    public Task<HttpResponseMessage> PutAsync(string url, object? body = null) =>
        SendAsync(HttpMethod.Put, url, body);

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await http.SendAsync(request);
    }

    /// <summary>Başarılı girişte belirteçleri saklar ve sonraki isteklere Bearer olarak ekler.</summary>
    public async Task<TokenPair> LoginAsync(string email, string password)
    {
        var response = await PostAsync("/api/auth/login", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();

        var json = await ReadJsonAsync(response);
        return UseTokens(json);
    }

    /// <summary>Yenileme belirteciyle belirteç çifti alır ve saklar.</summary>
    public async Task<HttpResponseMessage> RefreshAsync(string? refreshToken = null)
    {
        var response = await PostAsync("/api/auth/refresh", new { RefreshToken = refreshToken ?? RefreshToken });

        if (response.IsSuccessStatusCode)
        {
            UseTokens(await ReadJsonAsync(response));
        }

        return response;
    }

    public void SetAccessToken(string? token)
    {
        AccessToken = token;
        http.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
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

    private TokenPair UseTokens(JsonElement json)
    {
        var pair = new TokenPair(
            json.GetProperty("accessToken").GetString()!,
            json.GetProperty("refreshToken").GetString()!);

        RefreshToken = pair.RefreshToken;
        SetAccessToken(pair.AccessToken);
        return pair;
    }
}
