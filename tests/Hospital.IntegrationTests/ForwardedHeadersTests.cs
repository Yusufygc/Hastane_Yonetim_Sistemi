using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// Ters vekil arkasında (docker-compose'ta nginx) hız sınırı vekilin IP'sine değil gerçek istemciye göre uygulanmalı;
/// ama başlık yalnızca güvenilen vekilden geliyorsa dikkate alınmalı (aksi halde istemci taklit edip sınırı atlatır).
/// </summary>
public class ForwardedHeadersTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    /// <summary>TestServer'da bağlantı IP'si yoktur; vekil gibi davranması için en dışta sabit bir IP atanır.</summary>
    private sealed class FakeRemoteIpFilter(string ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
                await nextMiddleware();
            });
            next(app);
        };
    }

    private WebApplicationFactory<Program> FactoryBehind(string proxyIp, Dictionary<string, string?> settings)
    {
        settings["RateLimiting:Auth:PermitLimit"] = "2";
        return fixture.CreateFactory(settings).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new FakeRemoteIpFilter(proxyIp))));
    }

    private static async Task<HttpStatusCode> LoginAs(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent("""{"email":"yok@example.test","password":"YanlisParola1"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Guvenilen_vekilden_gelen_istekte_hiz_siniri_gercek_istemciye_gore_ayrilir()
    {
        await using var factory = FactoryBehind("172.18.0.5", new()
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:TrustedNetworks:0"] = "172.16.0.0/12",
        });
        var client = factory.CreateClient();

        // Birinci istemcinin kotası dolar (limit 2) ...
        (await LoginAs(client, "203.0.113.1")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAs(client, "203.0.113.1")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAs(client, "203.0.113.1")).Should().Be(HttpStatusCode.TooManyRequests);

        // ... ikinci istemci aynı vekilden gelse de etkilenmez.
        (await LoginAs(client, "203.0.113.2")).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Guvenilmeyen_kaynaktan_gelen_X_Forwarded_For_yok_sayilir_taklit_ile_sinir_asilamaz()
    {
        // Bağlantı 8.8.8.8'den geliyor: güvenilen ağda değil, başlık yok sayılır → herkes tek kovada.
        await using var factory = FactoryBehind("8.8.8.8", new()
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:TrustedNetworks:0"] = "172.16.0.0/12",
        });
        var client = factory.CreateClient();

        (await LoginAs(client, "203.0.113.1")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAs(client, "203.0.113.2")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAs(client, "203.0.113.3")).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Varsayilan_kapali_X_Forwarded_For_dikkate_alinmaz()
    {
        await using var factory = FactoryBehind("172.18.0.5", new());
        var client = factory.CreateClient();

        (await LoginAs(client, "203.0.113.1")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAs(client, "203.0.113.2")).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAs(client, "203.0.113.3")).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData(null, "TrustedNetworks veya")]       // etkin ama güvenilen vekil yok → açılışta açık hata
    [InlineData("gecersiz", "geçersiz CIDR")]        // bozuk CIDR → açılışta açık hata
    public async Task Hatali_yapilandirma_uygulama_acilirken_acik_hata_verir(string? cidr, string expectedMessagePart)
    {
        var settings = new Dictionary<string, string?> { ["ForwardedHeaders:Enabled"] = "true" };
        if (cidr is not null)
        {
            settings["ForwardedHeaders:TrustedNetworks:0"] = cidr;
        }

        await using var factory = fixture.CreateFactory(settings);

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().Which.ToString().Should().Contain(expectedMessagePart);
    }
}
