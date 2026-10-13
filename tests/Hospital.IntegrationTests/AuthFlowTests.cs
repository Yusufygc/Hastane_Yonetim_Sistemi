using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Hospital.Domain.Entities.Users;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

public class AuthFlowTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Kayit_basarili_olursa_201_doner_ve_CSRF_belirteci_istemez()
    {
        var client = fixture.CreateClient();

        // Bearer (JWT) kullanıldığı için çerez/CSRF yoktur: yazma isteği başlıksız çalışır.
        var response = await client.PostAsync("/api/auth/register", TestAccount.New().RegistrationBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // CSRF uç noktası artık yok (girişli kullanıcı için 404; anonim istekte varsayılan kapalı politika 401 verir).
        await client.RegisterAndLoginAsync();
        (await client.GetAsync("/api/auth/csrf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ayni_eposta_ile_ikinci_kayit_409_doner()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        var second = TestAccount.New() with { Email = account.Email.ToUpperInvariant() };
        var response = await client.PostAsync("/api/auth/register", second.RegistrationBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).Should().Be("auth.email_taken");
    }

    [Fact]
    public async Task Ayni_TC_ile_ikinci_kayit_409_doner()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        var second = TestAccount.New() with { IdentityNumber = account.IdentityNumber };
        var response = await client.PostAsync("/api/auth/register", second.RegistrationBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).Should().Be("auth.identity_number_taken");
    }

    [Fact]
    public async Task Zayif_parola_ve_gecersiz_TC_alan_bazli_400_doner()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New() with { Password = "kisa", IdentityNumber = "12345678901" };

        var response = await client.PostAsync("/api/auth/register", account.RegistrationBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors");
        errors.TryGetProperty("password", out _).Should().BeTrue();
        errors.TryGetProperty("identityNumber", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Giris_basarili_olursa_erisim_ve_yenileme_belirteci_doner_me_hastayi_dondurur()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        client.AccessToken.Should().NotBeNullOrWhiteSpace();
        client.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("role").GetString().Should().Be("PATIENT");
        body.RootElement.GetProperty("email").GetString().Should().Be(account.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task Giris_yapmadan_me_401_doner()
    {
        var response = await fixture.CreateClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Yanlis_parola_ve_bilinmeyen_eposta_ayni_401_yanitini_verir()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        var wrongPassword = await client.PostAsync("/api/auth/login", account.LoginBody("Yanlis1234"));
        var unknownEmail = await client.PostAsync("/api/auth/login", TestAccount.New().LoginBody());

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(wrongPassword)).Should().Be("auth.invalid_credentials");
        (await ProblemCodeAsync(unknownEmail)).Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Ardisik_basarisiz_denemelerden_sonra_dogru_parola_bile_reddedilir()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await client.PostAsync("/api/auth/login", account.LoginBody("Yanlis1234")))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var response = await client.PostAsync("/api/auth/login", account.LoginBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hesap_pasife_alininca_acik_erisim_belirteci_aninda_gecersiz_olur()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
            user.Deactivate();
            await db.SaveChangesAsync();
        }

        // Belirtecin süresi dolmadı, ama hesap pasif: security_stamp değişti.
        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Parola_degisince_security_stamp_degisir_ve_eski_erisim_belirteci_gecersiz_olur()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
            user.ChangePasswordHash(user.PasswordHash + "x");
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hasta_hesabi_TPT_ile_iki_tabloya_yazilir()
    {
        var client = fixture.CreateClient();
        var account = TestAccount.New();
        (await client.PostAsync("/api/auth/register", account.RegistrationBody())).EnsureSuccessStatusCode();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == account.Email.ToLowerInvariant());

        user.Should().BeOfType<Patient>();
        user.Role.Should().Be(UserRole.Patient);
        (await db.Patients.AsNoTracking().AnyAsync(p => p.Id == user.Id && p.IdentityNumber == account.IdentityNumber))
            .Should().BeTrue();
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
