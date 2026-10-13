using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// Hata yanıtları: geçersiz gövde/parametre, doğrulama, iş kuralı, beklenmeyen hata, 401/403/404/405/415/429.
/// Hepsi aynı biçimde (ProblemDetails: title, status, code, traceId, varsa errors) ve anlaşılır Türkçe mesajla dönmeli.
/// </summary>
public class RequestErrorTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;
    private readonly HospitalApiFactory _factory;

    public RequestErrorTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _factory = fixture.CreateFactory(includeProbeEndpoints: true);
    }

    private ApiClient NewClient() => new(_factory.CreateClient());

    private static async Task<(string? Code, System.Text.Json.JsonElement Body)> ReadProblemAsync(HttpResponseMessage response)
    {
        var body = await ApiClient.ReadJsonAsync(response);
        var code = body.TryGetProperty("code", out var c) ? c.GetString() : null;
        return (code, body);
    }

    private static string ErrorText(System.Text.Json.JsonElement body, string field) =>
        string.Join(" | ", body.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()));

    // ---------- Kullanıcının gönderdiği gerçek gövde: adım adım düzeltme ----------

    [Fact]
    public async Task Hatali_tarih_formati_hangi_alanin_neden_hatali_oldugunu_soyler()
    {
        var client = NewClient();

        // Gerçek istek: birthDate "11.11.1999" (yyyy-MM-dd değil), phone sayı, identityNumber "1", parola kısa.
        var response = await client.PostAsync("/api/auth/register", new
        {
            email = "deneme@gmail.com",
            password = "deneme",
            firstName = "deneme",
            lastName = "deneme",
            identityNumber = "1",
            birthDate = "11.11.1999",
            phone = 5444444444,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.invalid_body");
        body.GetProperty("title").GetString().Should().Be("İstek gövdesi geçersiz");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        ErrorText(body, "birthDate").Should().Contain("yyyy-MM-dd").And.Contain("1999-11-11");
    }

    [Fact]
    public async Task Telefon_sayi_olarak_gonderilirse_metin_olmali_mesaji_doner()
    {
        var client = NewClient();

        var response = await client.PostAsync("/api/auth/register", new
        {
            email = "deneme@gmail.com",
            password = "Deneme123x",
            firstName = "deneme",
            lastName = "deneme",
            identityNumber = "10000000146",
            birthDate = "1999-11-11",
            phone = 5444444444,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.invalid_body");
        ErrorText(body, "phone").Should().Contain("Metin olmalı").And.Contain("tırnak");
    }

    [Fact]
    public async Task Gecerli_tipli_ama_gecersiz_degerler_tum_dogrulama_hatalarini_tek_seferde_doner()
    {
        var client = NewClient();

        var response = await client.PostAsync("/api/auth/register", new
        {
            email = "deneme@gmail.com",
            password = "deneme",
            firstName = "deneme",
            lastName = "deneme",
            identityNumber = "1",
            birthDate = "1999-11-11",
            phone = "5444444444",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("validation.failed");

        // Alan adları JSON'daki gibi camelCase; her alan için ayrı, anlaşılır mesaj.
        ErrorText(body, "password").Should().Contain("en az 8 karakter");
        ErrorText(body, "password").Should().Contain("harf ve bir rakam");
        ErrorText(body, "identityNumber").Should().Contain("T.C. kimlik numarası geçersiz");
        body.GetProperty("errors").TryGetProperty("email", out _).Should().BeFalse("e-posta geçerli");
    }

    [Fact]
    public async Task Ic_ice_liste_alanlarinin_dogrulama_anahtari_yol_icerir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(_fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);

        var response = await superAdmin.PutAsync($"/api/hospitals/{hospitalId}/working-hours", new
        {
            hours = new[] { new { day = 1, openTime = "17:00", closeTime = "08:00" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (_, body) = await ReadProblemAsync(response);
        ErrorText(body, "hours[0].closeTime").Should().Contain("Kapanış saati açılış saatinden sonra");
    }

    // ---------- JSON / gövde / içerik türü ----------

    [Fact]
    public async Task Bozuk_JSON_sozdizimi_satir_ve_konum_bildirir()
    {
        var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = new StringContent("{ \"email\": \"a@b.com\", }", Encoding.UTF8, "application/json"),
        };

        var response = await client.Http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.invalid_json");
        ErrorText(body, "body").Should().Contain("JSON sözdizimi hatalı").And.Contain("satır 1");
    }

    [Fact]
    public async Task Bos_govde_gonderilirse_govde_zorunlu_hatasi_doner()
    {
        var client = NewClient();

        var response = await client.PostAsync("/api/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.body_required");
        ErrorText(body, "body").Should().Contain("zorunlu");
    }

    [Fact]
    public async Task Json_olmayan_icerik_turu_415_ve_aciklama_doner()
    {
        var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent("email=a&password=b", Encoding.UTF8, "text/plain"),
        };

        var response = await client.Http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType, await response.Content.ReadAsStringAsync());
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.unsupported_media_type");
        body.GetProperty("detail").GetString().Should().Contain("application/json");
    }

    [Theory]
    [InlineData("""{"id":"abc","date":"2026-01-01","text":"x","count":1}""", "id", "GUID")]
    [InlineData("""{"id":null,"date":"01/02/2026","text":"x","count":1}""", "date", "yyyy-MM-dd")]
    [InlineData("""{"id":null,"date":"2026-01-01","text":12,"count":1}""", "text", "Metin olmalı")]
    [InlineData("""{"id":null,"date":"2026-01-01","text":"x","count":"bir"}""", "count", "Tam sayı")]
    [InlineData("""{"id":null,"date":"2026-01-01","text":"x","count":1.5}""", "count", "Tam sayı")]
    public async Task Her_tur_hatasi_dogru_alani_ve_beklenen_turu_soyler(string json, string field, string expectedFragment)
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test/error/body")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", client.AccessToken);

        var response = await client.Http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.invalid_body");
        ErrorText(body, field).Should().Contain(expectedFragment);
    }

    [Fact]
    public async Task Gecersiz_sorgu_parametresi_parametre_adini_ve_degeri_soyler()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var invalid = await client.GetAsync("/api/test/error/query?id=abc&count=1");
        var missing = await client.GetAsync($"/api/test/error/query?id={Guid.NewGuid()}");

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (invalidCode, invalidBody) = await ReadProblemAsync(invalid);
        invalidCode.Should().Be("request.invalid_parameter");
        ErrorText(invalidBody, "id").Should().Contain("'abc'").And.Contain("GUID");

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (_, missingBody) = await ReadProblemAsync(missing);
        ErrorText(missingBody, "count").Should().Contain("zorunludur");
    }

    // ---------- İş kuralı ve beklenmeyen hata ----------

    [Fact]
    public async Task Is_kurali_ihlali_422_ve_okunabilir_mesaj_doner()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync("/api/test/error/domain");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("domain.rule_violation");
        body.GetProperty("detail").GetString().Should().Be("Kural ihlali testi.");
    }

    [Fact]
    public async Task Beklenmeyen_hata_500_genel_mesaj_ve_traceId_doner_yigin_izi_sizmaz()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync("/api/test/error/unhandled");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("server.error");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().Contain("traceId");
        body.TryGetProperty("stackTrace", out _).Should().BeFalse();

        // Test ortamı Development: neyin patladığı geliştiriciye gösterilir (üretimde eklenmez).
        body.GetProperty("exceptionType").GetString().Should().Be("System.InvalidOperationException");
    }

    // ---------- 401 / 403 / 404 / 405: gövdesiz kalan hatalar ----------

    [Fact]
    public async Task Token_olmadan_401_gövdesi_nasil_giris_yapilacagini_soyler()
    {
        var response = await NewClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("auth.unauthorized");
        body.GetProperty("detail").GetString().Should().Contain("Authorization: Bearer");
    }

    [Fact]
    public async Task Suresi_dolmus_token_401_gövdesi_yenilemeyi_onerir()
    {
        var (userId, stamp) = await RegisteredUserAsync();
        var expired = TestJwt.Create(TestJwt.UserClaims(userId, "PATIENT", stamp), expires: DateTime.UtcNow.AddHours(-1));

        var response = await MeWithRawTokenAsync(expired);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("auth.unauthorized");
        body.GetProperty("detail").GetString().Should().Contain("süresi dolmuş").And.Contain("/api/auth/refresh");
    }

    [Fact]
    public async Task Imzasi_gecersiz_token_401_gövdesi_imzayi_belirtir()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var response = await MeWithRawTokenAsync(TestJwt.Tamper(client.AccessToken!));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (_, body) = await ReadProblemAsync(response);
        body.GetProperty("detail").GetString().Should().Contain("imza");
    }

    [Fact]
    public async Task Yetersiz_rol_403_gövdesi_auth_forbidden_kodu_tasir()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync("/api/hospitals");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("auth.forbidden");
        body.GetProperty("title").GetString().Should().Be("Bu işlem için yetkiniz yok");
    }

    [Fact]
    public async Task Olmayan_adres_404_gövdesi_adresi_belirtir()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync("/api/olmayan-uc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.not_found");
        body.GetProperty("detail").GetString().Should().Contain("/api/olmayan-uc");
    }

    [Fact]
    public async Task Desteklenmeyen_http_metodu_405_gövdesi_doner()
    {
        var client = NewClient();
        await client.RegisterAndLoginAsync();

        var response = await client.PostAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        var (code, _) = await ReadProblemAsync(response);
        code.Should().Be("request.method_not_allowed");
    }

    // ---------- 429 ----------

    [Fact]
    public async Task Hiz_siniri_asilinca_429_gövdesi_ve_Retry_After_basligi_doner()
    {
        var limited = _fixture.CreateFactory(new Dictionary<string, string?> { ["RateLimiting:Auth:PermitLimit"] = "1" });
        var client = new ApiClient(limited.CreateClient());
        var account = TestAccount.New();

        await client.PostAsync("/api/auth/login", account.LoginBody());   // izin verilen tek istek
        var response = await client.PostAsync("/api/auth/login", account.LoginBody());

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("rate_limit.exceeded");
        body.GetProperty("detail").GetString().Should().Contain("saniye sonra");
    }

    // ---------- Üretim ortamı ----------

    [Fact]
    public async Task Uretim_ortaminda_da_bozuk_govde_alan_bazli_hata_doner()
    {
        // ASP.NET Core varsayılanında gövde/parametre hatası istisnası yalnızca Development'ta fırlatılır;
        // üretimde kapalı olsaydı bu hata ayrıntısız, gövdesiz 400 olurdu.
        var production = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = new ApiClient(production.CreateClient());

        var response = await client.PostAsync("/api/auth/register", new
        {
            email = "deneme@gmail.com",
            password = "Deneme123x",
            firstName = "deneme",
            lastName = "deneme",
            identityNumber = "10000000146",
            birthDate = "11.11.1999",
            phone = "5444444444",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("request.invalid_body");
        ErrorText(body, "birthDate").Should().Contain("yyyy-MM-dd");
    }

    [Fact]
    public async Task Uretim_ortaminda_500_yanitina_istisna_ayrintisi_eklenmez()
    {
        var production = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = new ApiClient(production.CreateClient());
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync("/api/test/error/unhandled");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var (code, body) = await ReadProblemAsync(response);
        code.Should().Be("server.error");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("exceptionType", out _).Should().BeFalse();
        body.TryGetProperty("exceptionMessage", out _).Should().BeFalse();
        body.GetRawText().Should().NotContain("InvalidOperationException").And.NotContain("Beklenmeyen test hatası");
    }

    // ---------- Yardımcılar ----------

    private async Task<HttpResponseMessage> MeWithRawTokenAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<(Guid UserId, string Stamp)> RegisteredUserAsync()
    {
        var client = NewClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
        return (user.Id, user.SecurityStamp);
    }
}
