using System.Net;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Hospital.Domain.Entities.Notifications;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// "Şifremi unuttum": e-postaya tek kullanımlık, kısa ömürlü bağlantı; hesap varlığı sızdırılmaz; kullanılınca tüm oturumlar
/// kapanır, kilit kalkar ve belirteç bir daha çalışmaz. E-postalar outbox satırı olarak doğrulanır (SMTP gerekmez).
/// </summary>
public partial class PasswordResetTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string NewPassword = "YeniParola9x";

    [GeneratedRegex(@"token=(?<t>[A-Za-z0-9_\-%.]+)")]
    private static partial Regex TokenPattern();

    private static string TokenFrom(EmailOutboxMessage mail)
    {
        var match = TokenPattern().Match(mail.Body);
        match.Success.Should().BeTrue("e-posta sıfırlama bağlantısı içermeli");
        return Uri.UnescapeDataString(match.Groups["t"].Value);
    }

    private async Task<(ApiClient Client, TestAccount Account)> NewPatientAsync()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();
        return (client, account);
    }

    private async Task<List<EmailOutboxMessage>> ResetMailsAsync(string email) =>
        (await OutboxKit.ToAsync(fixture.Factory, email)).Where(m => m.TemplateKey == "password-reset-requested").ToList();

    private static async Task<string> CodeOfAsync(HttpResponseMessage response) =>
        (await ApiClient.ReadJsonAsync(response)).GetProperty("code").GetString()!;

    // ---------- İstek ----------

    [Fact]
    public async Task Kayitli_hesaba_baglantili_eposta_outboxa_eklenir_belirtec_ozet_olarak_saklanir()
    {
        var (session, account) = await NewPatientAsync();
        var userId = await OutboxKit.IdOfAsync(session);

        var response = await fixture.CreateClient().PostAsync("/api/auth/forgot-password", new { account.Email });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var mail = (await ResetMailsAsync(account.Email)).Should().ContainSingle().Subject;
        mail.Status.Should().Be(EmailStatus.Pending);
        mail.Subject.Should().Be("Parola sıfırlama isteği");
        mail.Body.Should().Contain("http://localhost:5173/sifre-sifirla?token=").And.Contain("30 dakika").And.NotContain(account.Password);

        // Veritabanında düz belirteç yok, yalnız SHA-256 özeti (64 hex).
        var token = TokenFrom(mail);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == userId && t.UsedAt == null);
        stored.TokenHash.Should().NotBe(token).And.MatchRegex("^[0-9a-f]{64}$");
        (stored.ExpiresAt - stored.CreatedAt).Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Bilinmeyen_eposta_ayni_202_yaniti_verir_ve_hicbir_sey_olusturmaz()
    {
        var anonymous = fixture.CreateClient();
        var known = (await NewPatientAsync()).Account;

        var unknown = await anonymous.PostAsync("/api/auth/forgot-password", new { Email = $"yok.{Guid.NewGuid():N}@example.test" });
        var existing = await anonymous.PostAsync("/api/auth/forgot-password", new { known.Email });

        // Hesap varlığı yanıttan anlaşılamaz: durum ve gövde aynı.
        unknown.StatusCode.Should().Be(existing.StatusCode).And.Be(HttpStatusCode.Accepted);
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await existing.Content.ReadAsStringAsync());

        (await OutboxKit.RowsAsync(fixture.Factory)).Should().NotContain(m => m.ToAddress.StartsWith("yok."));
    }

    [Fact]
    public async Task Pasif_hesaba_eposta_gitmez_yanit_yine_202()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var (client, account) = await NewPatientAsync();
        var patientId = await OutboxKit.IdOfAsync(client);
        (await superAdmin.PostAsync($"/api/users/{patientId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await fixture.CreateClient().PostAsync("/api/auth/forgot-password", new { account.Email });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ResetMailsAsync(account.Email)).Should().BeEmpty();
    }

    [Fact]
    public async Task Gecersiz_eposta_bicimi_400_dogrulama_hatasidir()
    {
        var response = await fixture.CreateClient().PostAsync("/api/auth/forgot-password", new { Email = "eposta-degil" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Kisa_surede_tekrarlanan_istek_ikinci_eposta_uretmez_eposta_bombardimani_engellenir()
    {
        var (_, account) = await NewPatientAsync();
        var anonymous = fixture.CreateClient();

        (await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        (await ResetMailsAsync(account.Email)).Should().ContainSingle();
    }

    [Fact]
    public async Task Yeni_baglanti_istenince_eskisi_gecersiz_olur_yalniz_en_yeni_calisir()
    {
        await using var factory = fixture.CreateFactory(new Dictionary<string, string?> { ["PasswordReset:ResendIntervalSeconds"] = "0" });
        var anonymous = new ApiClient(factory.CreateClient());
        var (_, account) = await NewPatientAsync();

        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var mails = await ResetMailsAsync(account.Email);
        mails.Should().HaveCount(2);
        var oldToken = TokenFrom(mails[0]);
        var newToken = TokenFrom(mails[1]);

        var withOld = await anonymous.PostAsync("/api/auth/reset-password", new { Token = oldToken, NewPassword });
        withOld.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOfAsync(withOld)).Should().Be("auth.invalid_reset_token");

        (await anonymous.PostAsync("/api/auth/reset-password", new { Token = newToken, NewPassword })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- Sıfırlama ----------

    [Fact]
    public async Task Sifirlama_parolayi_degistirir_eski_oturumlari_kapatir_ve_bildirim_gonderir()
    {
        var (session, account) = await NewPatientAsync();
        var oldAccess = session.AccessToken!;
        var oldRefresh = session.RefreshToken!;
        var anonymous = fixture.CreateClient();
        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var token = TokenFrom((await ResetMailsAsync(account.Email)).Single());

        var reset = await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Eski parola çalışmaz, yenisi çalışır.
        (await anonymous.PostAsync("/api/auth/login", account.LoginBody())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/auth/login", account.LoginBody(NewPassword))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Açık oturumlar kapanır: eski yenileme belirteci iptal, eski erişim belirteci güvenlik damgası yüzünden geçersiz.
        (await fixture.CreateClient().RefreshAsync(oldRefresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var stale = fixture.CreateClient();
        stale.SetAccessToken(oldAccess);
        (await stale.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Hesap sahibine "parolanız değişti" bildirimi gider; e-postada parola yok.
        var changed = (await OutboxKit.ToAsync(fixture.Factory, account.Email)).Should().ContainSingle(m => m.TemplateKey == "password-changed").Subject;
        changed.Subject.Should().Be("Parolanız değiştirildi");
        changed.Body.Should().NotContain(NewPassword).And.NotContain(token);
    }

    [Fact]
    public async Task Belirtec_tek_kullanimliktir_ikinci_kullanim_400()
    {
        var (_, account) = await NewPatientAsync();
        var anonymous = fixture.CreateClient();
        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var token = TokenFrom((await ResetMailsAsync(account.Email)).Single());

        (await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword = "BaskaParola7y" });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOfAsync(second)).Should().Be("auth.invalid_reset_token");
        // İkinci denemedeki parola kabul edilmedi.
        (await anonymous.PostAsync("/api/auth/login", account.LoginBody("BaskaParola7y"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Suresi_dolmus_belirtec_reddedilir()
    {
        // Ömür ~60 ms: bağlantı üretilir, kısa bekleyince süresi dolar.
        await using var factory = fixture.CreateFactory(new Dictionary<string, string?> { ["PasswordReset:LifetimeMinutes"] = "0.001" });
        var anonymous = new ApiClient(factory.CreateClient());
        var (_, account) = await NewPatientAsync();
        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var token = TokenFrom((await ResetMailsAsync(account.Email)).Single());
        await Task.Delay(300);

        var response = await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOfAsync(response)).Should().Be("auth.invalid_reset_token");
    }

    [Theory]
    [InlineData("tamamen-uydurma-belirtec")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Bilinmeyen_belirtec_ayni_hatayi_verir(string token)
    {
        var response = await fixture.CreateClient().PostAsync("/api/auth/reset-password", new { Token = token, NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOfAsync(response)).Should().Be("auth.invalid_reset_token");
    }

    [Fact]
    public async Task Zayif_parola_reddedilir_ve_belirtec_harcanmaz()
    {
        var (_, account) = await NewPatientAsync();
        var anonymous = fixture.CreateClient();
        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var token = TokenFrom((await ResetMailsAsync(account.Email)).Single());

        var weak = await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword = "kisa" });
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(weak)).GetProperty("errors").TryGetProperty("newPassword", out _).Should().BeTrue();

        // Doğrulama hatası belirteci tüketmedi: geçerli parolayla hâlâ kullanılabilir.
        (await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Kilitli_hesap_sifirlama_sonrasi_giris_yapabilir()
    {
        var (_, account) = await NewPatientAsync();
        var anonymous = fixture.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await anonymous.PostAsync("/api/auth/login", account.LoginBody("YanlisParola1"));
        }

        // Kilitliyken doğru parola bile reddedilir.
        (await anonymous.PostAsync("/api/auth/login", account.LoginBody())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var token = TokenFrom((await ResetMailsAsync(account.Email)).Single());
        (await anonymous.PostAsync("/api/auth/reset-password", new { Token = token, NewPassword })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.PostAsync("/api/auth/login", account.LoginBody(NewPassword))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Eszamanli_iki_sifirlama_yalniz_biri_basarili_olur()
    {
        var (_, account) = await NewPatientAsync();
        var anonymous = fixture.CreateClient();
        await anonymous.PostAsync("/api/auth/forgot-password", new { account.Email });
        var token = TokenFrom((await ResetMailsAsync(account.Email)).Single());

        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            fixture.CreateClient().PostAsync("/api/auth/reset-password", new { Token = token, NewPassword = $"Parola{i}Eszamanli" })));

        // Tek kullanım: tam bir başarı; diğerleri 400 (kullanılmış) veya 409 (aynı anda yarıştı).
        attempts.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        attempts.Where(r => r.StatusCode != HttpStatusCode.NoContent).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.Conflict);
    }
}
