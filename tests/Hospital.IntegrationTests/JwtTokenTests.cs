using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using AwesomeAssertions;
using Hospital.Application.Features.Auth;
using Hospital.Domain.Entities.Users;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Hospital.IntegrationTests;

/// <summary>JWT erişim belirteci ve yenileme belirteci (rotation, yeniden kullanım tespiti, iptal) davranışları.</summary>
public class JwtTokenTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    // ---------- Erişim belirteci (JWT) ----------

    [Fact]
    public async Task Giris_yaniti_gecerli_HS256_JWT_ve_beklenen_claimleri_icerir()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/api/auth/login", account.LoginBody());
        var json = await ApiClient.ReadJsonAsync(response);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(json.GetProperty("accessToken").GetString()!);
        jwt.Alg.Should().Be("HS256");
        jwt.Issuer.Should().Be(TestJwt.Issuer);
        jwt.Audiences.Should().ContainSingle().Which.Should().Be(TestJwt.Audience);
        jwt.GetClaim("role").Value.Should().Be("PATIENT");
        jwt.GetClaim("email").Value.Should().Be(account.Email.ToLowerInvariant());
        jwt.GetClaim("security_stamp").Value.Should().NotBeNullOrWhiteSpace();
        jwt.Subject.Should().NotBeNullOrWhiteSpace();

        // Kısa ömür (varsayılan 15 dk) ve yenileme belirteci ayrı, uzun ömürlü.
        (jwt.ValidTo - DateTime.UtcNow).Should().BeGreaterThan(TimeSpan.FromMinutes(10)).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(16));
        json.GetProperty("accessTokenExpiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
        json.GetProperty("refreshTokenExpiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow.AddDays(6));
        json.GetProperty("refreshToken").GetString().Should().NotBe(json.GetProperty("accessToken").GetString());
        json.GetProperty("user").GetProperty("role").GetString().Should().Be("PATIENT");
    }

    [Fact]
    public async Task Gecerli_belirteç_ile_korunan_uca_erisilir_belirteç_yoksa_401()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        client.SetAccessToken(null);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cerez_artik_kimlik_dogrulamaz_yalnizca_Authorization_basligi_gecerlidir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        // Aynı belirteci Authorization yerine Cookie ile göndermek işe yaramaz.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", $"hospital.auth={client.AccessToken}");
        var response = await fixture.Factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("tamamen-bozuk")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public async Task Bozuk_belirteç_401_doner(string token)
    {
        var response = await MeWithRawTokenAsync(token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Imzasi_bozulmus_belirteç_reddedilir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        var response = await MeWithRawTokenAsync(TestJwt.Tamper(client.AccessToken!));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Baska_anahtarla_imzalanmis_belirteç_reddedilir()
    {
        var (userId, stamp) = await RegisteredUserAsync();
        var forged = TestJwt.Create(TestJwt.UserClaims(userId, "PATIENT", stamp), signingKey: "baska-bir-anahtar-0123456789-abcdefghijklmnopqrstuvwxyz");

        (await MeWithRawTokenAsync(forged)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dogru_anahtarla_imzali_gecerli_belirteç_kabul_edilir_kontrol_testi()
    {
        // Sahte belirteç üretecinin kendisi doğru çalışıyor: diğer reddetme testlerinin anlamlı olmasını sağlar.
        var (userId, stamp) = await RegisteredUserAsync();
        var valid = TestJwt.Create(TestJwt.UserClaims(userId, "PATIENT", stamp));

        (await MeWithRawTokenAsync(valid)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Suresi_dolmus_belirteç_reddedilir()
    {
        var (userId, stamp) = await RegisteredUserAsync();
        var expired = TestJwt.Create(TestJwt.UserClaims(userId, "PATIENT", stamp), expires: DateTime.UtcNow.AddHours(-1));

        (await MeWithRawTokenAsync(expired)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("baska-issuer", null)]
    [InlineData(null, "baska-audience")]
    public async Task Yanlis_issuer_veya_audience_reddedilir(string? issuer, string? audience)
    {
        var (userId, stamp) = await RegisteredUserAsync();
        var token = TestJwt.Create(TestJwt.UserClaims(userId, "PATIENT", stamp), issuer: issuer, audience: audience);

        (await MeWithRawTokenAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Guvenlik_damgasi_uyusmayan_belirteç_reddedilir_yetki_yukseltme_denemesi()
    {
        var (userId, _) = await RegisteredUserAsync();
        var token = TestJwt.Create(TestJwt.UserClaims(userId, "ADMIN", "yanlis-damga"));

        (await MeWithRawTokenAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hasta_belirtecine_ADMIN_rolu_claimi_eklense_bile_imza_gecersiz_oldugu_icin_reddedilir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        // Gerçek belirteçten rolü değiştirmek imzayı bozar.
        var parts = client.AccessToken!.Split('.');
        var forgedPayload = Convert.ToBase64String("{\"sub\":\"x\",\"role\":\"ADMIN\"}"u8.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var forged = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        (await MeWithRawTokenAsync(forged)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- Yenileme belirteci ----------

    [Fact]
    public async Task Yenileme_yeni_belirteç_cifti_verir_ve_eski_yenileme_belirteci_iptal_olur()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();
        var first = new TokenPair(client.AccessToken!, client.RefreshToken!);

        var response = await client.RefreshAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        client.RefreshToken.Should().NotBe(first.RefreshToken);
        client.AccessToken.Should().NotBe(first.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Iptal_edilmis_yenileme_belirtecinin_yeniden_kullanimi_tum_oturumlari_kapatir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();
        var stolen = client.RefreshToken!;

        (await client.RefreshAsync()).StatusCode.Should().Be(HttpStatusCode.OK);   // meşru yenileme
        var latest = client.RefreshToken!;

        var replay = await fixture.CreateClient().RefreshAsync(stolen);             // saldırgan eskiyi tekrar sunar

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ApiClient.ReadJsonAsync(replay)).GetProperty("code").GetString().Should().Be("auth.invalid_refresh_token");

        // Çalınma şüphesi: meşru kullanıcının güncel belirteci de iptal edilmiş olmalı.
        (await fixture.CreateClient().RefreshAsync(latest)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bilinmeyen_yenileme_belirteci_401_bos_olan_400_doner()
    {
        var client = fixture.CreateClient();

        (await client.RefreshAsync("bilinmeyen-belirteç")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/auth/refresh", new { RefreshToken = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Suresi_dolmus_yenileme_belirteci_reddedilir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hash = RefreshTokenSecret.Hash(client.RefreshToken!);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE refresh_token SET expires_at = now() - interval '1 minute' WHERE token_hash = {0}", hash);
        }

        (await client.RefreshAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Yenileme_belirteci_veritabaninda_yalnizca_ozet_olarak_saklanir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hashes = await db.RefreshTokens.AsNoTracking().Select(t => t.TokenHash).ToListAsync();

        hashes.Should().NotContain(client.RefreshToken!);
        hashes.Should().Contain(RefreshTokenSecret.Hash(client.RefreshToken!));
    }

    [Fact]
    public async Task Pasife_alinan_kullanicinin_yenileme_belirteci_reddedilir()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
            user.Deactivate();
            await db.SaveChangesAsync();
        }

        (await client.RefreshAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- Çıkış ----------

    [Fact]
    public async Task Cikis_yenileme_belirtecini_iptal_eder_boylece_yenileme_artik_calismaz()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();
        var refresh = client.RefreshToken!;

        var logout = await client.PostAsync("/api/auth/logout", new { RefreshToken = refresh });

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await fixture.CreateClient().RefreshAsync(refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cikis_kimlik_gerektirir()
    {
        var response = await fixture.CreateClient().PostAsync("/api/auth/logout", new { RefreshToken = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Baskasinin_yenileme_belirtecini_cikis_ile_iptal_etmek_mumkun_degildir()
    {
        var victim = fixture.CreateClient();
        await victim.RegisterAndLoginAsync();
        var attacker = fixture.CreateClient();
        await attacker.RegisterAndLoginAsync();

        var response = await attacker.PostAsync("/api/auth/logout", new { RefreshToken = victim.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent); // varlık sızdırılmaz
        (await victim.RefreshAsync()).StatusCode.Should().Be(HttpStatusCode.OK); // kurbanın belirteci hâlâ geçerli
    }

    [Fact]
    public async Task Cikis_idempotenttir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();
        var refresh = client.RefreshToken!;

        (await client.PostAsync("/api/auth/logout", new { RefreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync("/api/auth/logout", new { RefreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- Yardımcılar ----------

    private async Task<HttpResponseMessage> MeWithRawTokenAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (token.Length > 0)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await fixture.Factory.CreateClient().SendAsync(request);
    }

    private async Task<(Guid UserId, string Stamp)> RegisteredUserAsync()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
        return (user.Id, user.SecurityStamp);
    }
}
