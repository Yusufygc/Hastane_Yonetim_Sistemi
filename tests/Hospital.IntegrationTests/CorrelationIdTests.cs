using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>İstek izleme: X-Correlation-Id her yanıtta döner ve hata yanıtlarındaki traceId ile aynıdır.</summary>
public class CorrelationIdTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Header = "X-Correlation-Id";

    private static string CorrelationOf(HttpResponseMessage response) => response.Headers.GetValues(Header).Single();

    [Fact]
    public async Task Baslik_gonderilmezse_uretilir_her_yanitta_doner()
    {
        var client = fixture.CreateClient();

        var health = await client.Http.GetAsync("/health");
        var catalog = await client.Http.GetAsync("/api/catalog/hospitals");

        CorrelationOf(health).Should().MatchRegex("^[0-9a-f]{32}$");
        CorrelationOf(catalog).Should().MatchRegex("^[0-9a-f]{32}$");
        CorrelationOf(health).Should().NotBe(CorrelationOf(catalog));
    }

    [Fact]
    public async Task Gecerli_istemci_kimligi_korunur()
    {
        var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/hospitals");
        request.Headers.Add(Header, "web-ui_2026.10-abc123");

        var response = await client.Http.SendAsync(request);

        CorrelationOf(response).Should().Be("web-ui_2026.10-abc123");
    }

    [Theory]
    [InlineData("has space inside")]
    [InlineData("bad<script>")]
    [InlineData("a;b,c")]
    public async Task Gecersiz_istemci_kimligi_yerine_yenisi_uretilir_log_enjeksiyonu_engellenir(string supplied)
    {
        var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/hospitals");
        request.Headers.TryAddWithoutValidation(Header, supplied);

        var response = await client.Http.SendAsync(request);

        CorrelationOf(response).Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task Cok_uzun_istemci_kimligi_reddedilir_yenisi_uretilir()
    {
        var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/hospitals");
        request.Headers.Add(Header, new string('a', 65));

        var response = await client.Http.SendAsync(request);

        CorrelationOf(response).Should().HaveLength(32);
    }

    [Fact]
    public async Task Hata_yanitlarinda_traceId_basliktaki_kimlikle_ayni_dir()
    {
        var anonymous = fixture.CreateClient();
        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        async Task AssertSameAsync(HttpResponseMessage response, HttpStatusCode status)
        {
            response.StatusCode.Should().Be(status);
            var body = await ApiClient.ReadJsonAsync(response);
            body.GetProperty("traceId").GetString().Should().Be(CorrelationOf(response));
        }

        await AssertSameAsync(await anonymous.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);       // 401 (gövdesiz → ProblemDetails)
        await AssertSameAsync(await patient.GetAsync("/api/hospitals"), HttpStatusCode.Forbidden);          // 403
        await AssertSameAsync(await patient.GetAsync("/api/bilinmeyen-rota"), HttpStatusCode.NotFound);     // 404
        await AssertSameAsync(await patient.PostAsync("/api/auth/change-password", new { }), HttpStatusCode.BadRequest); // 400 doğrulama
    }

    [Fact]
    public async Task Istemci_kimligi_hata_yanitina_da_yansir()
    {
        var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add(Header, "sikayet-4711");

        var response = await client.Http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("traceId").GetString().Should().Be("sikayet-4711");
        CorrelationOf(response).Should().Be("sikayet-4711");
    }
}
