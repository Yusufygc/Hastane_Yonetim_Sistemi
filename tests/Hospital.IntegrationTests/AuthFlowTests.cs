using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Hospital.Domain.Users;
using Hospital.Infrastructure.Persistence;
using Hospital.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

public class AuthFlowTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Kayit_basarili_olursa_201_doner()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/auth/register", TestAccount.New().RegistrationBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Kayit_CSRF_belirteci_olmadan_400_doner()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/auth/register", TestAccount.New().RegistrationBody(), withCsrf: false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).Should().Be("csrf.invalid");
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
        errors.TryGetProperty("Password", out _).Should().BeTrue();
        errors.TryGetProperty("IdentityNumber", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Giris_basarili_olursa_cookie_set_edilir_ve_me_hastayi_dondurur()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

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
    public async Task Cikis_yapinca_oturum_kapanir()
    {
        var client = fixture.CreateClient();
        await client.RegisterAndLoginAsync();

        var logout = await client.PostAsync("/api/auth/logout");
        var me = await client.GetAsync("/api/auth/me");

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hesap_pasife_alininca_acik_oturum_dusur()
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

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Parola_degisince_security_stamp_degisir_ve_eski_oturum_dusur()
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
