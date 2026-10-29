using AwesomeAssertions;
using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Auth.Commands.RequestPasswordReset;
using Hospital.Application.Features.Auth.Commands.ResetPassword;
using Hospital.Application.Features.Notifications;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Application;

public class PasswordResetUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    // ---------- Ayarlar ----------

    [Theory]
    [InlineData("")]
    [InlineData("localhost:5173")]       // şema yok
    [InlineData("ftp://example.test")]   // http(s) dışı
    [InlineData("/goreli/yol")]          // mutlak değil
    public void Arayuz_adresi_eksik_veya_gecersizse_acilista_hata_verir(string baseUrl)
    {
        var act = () => (PasswordResetSettings.Default with { FrontendBaseUrl = baseUrl }).Validated();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Frontend:BaseUrl");
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://randevu.hastane.example/")]
    public void Gecerli_arayuz_adresi_kabul_edilir(string baseUrl) =>
        (PasswordResetSettings.Default with { FrontendBaseUrl = baseUrl }).Validated().Should().NotBeNull();

    [Fact]
    public void Omur_pozitif_olmak_zorundadir()
    {
        var act = () => (PasswordResetSettings.Default with { FrontendBaseUrl = "http://localhost", TokenLifetime = TimeSpan.Zero }).Validated();

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("http://localhost:5173", "abc-DEF_123")]
    [InlineData("http://localhost:5173/", "abc-DEF_123")]   // sondaki eğik çizgi çift olmaz
    public void Baglanti_kokten_olusur_ve_belirteci_kodlar(string baseUrl, string token) =>
        (PasswordResetSettings.Default with { FrontendBaseUrl = baseUrl }).ResetLink(token)
            .Should().Be("http://localhost:5173/sifre-sifirla?token=abc-DEF_123");

    [Fact]
    public void Belirtecteki_ozel_karakterler_sorgu_dizesinde_kodlanir() =>
        (PasswordResetSettings.Default with { FrontendBaseUrl = "http://x" }).ResetLink("a b&c=d").Should().Be("http://x/sifre-sifirla?token=a%20b%26c%3Dd");

    // ---------- Belirteç varlığı ----------

    [Fact]
    public void Belirtec_omru_dolana_kadar_ve_kullanilmadikca_gecerlidir()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), new string('a', 64), Now, TimeSpan.FromMinutes(30));

        token.IsUsable(Now).Should().BeTrue();
        token.IsUsable(Now.AddMinutes(29)).Should().BeTrue();
        token.IsUsable(Now.AddMinutes(30)).Should().BeFalse();   // süre tam dolunca geçersiz
        token.IsUsable(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Kullanilan_belirtec_gecersizdir_ve_ilk_kullanim_zamani_korunur()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), new string('a', 64), Now, TimeSpan.FromMinutes(30));

        token.MarkUsed(Now.AddMinutes(1));
        token.MarkUsed(Now.AddMinutes(5)); // idempotent

        token.UsedAt.Should().Be(Now.AddMinutes(1));
        token.IsUsable(Now.AddMinutes(2)).Should().BeFalse();
    }

    [Fact]
    public void Gecersiz_omur_veya_bos_ozet_reddedilir()
    {
        ((Action)(() => PasswordResetToken.Issue(Guid.NewGuid(), "h", Now, TimeSpan.Zero))).Should().Throw<DomainException>();
        ((Action)(() => PasswordResetToken.Issue(Guid.NewGuid(), " ", Now, TimeSpan.FromMinutes(1)))).Should().Throw<DomainException>();
        ((Action)(() => PasswordResetToken.Issue(Guid.Empty, "h", Now, TimeSpan.FromMinutes(1)))).Should().Throw<DomainException>();
    }

    // ---------- Kullanıcı kilidi ----------

    [Fact]
    public void ClearLockout_basarisiz_giris_sayacini_ve_kilidi_kaldirir()
    {
        var patient = Patient.Register("a@b.test", "hash", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 1, 1), null, Now);
        for (var i = 0; i < 5; i++)
        {
            patient.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        }
        patient.IsLockedOut(Now).Should().BeTrue();

        patient.ClearLockout();

        patient.IsLockedOut(Now).Should().BeFalse();
        patient.FailedLoginCount.Should().Be(0);
    }

    // ---------- Doğrulayıcılar ----------

    [Theory]
    [InlineData("", false)]
    [InlineData("eposta-degil", false)]
    [InlineData("kisi@ornek.test", true)]
    public void Istek_dogrulayicisi_e_posta_bicimini_denetler(string email, bool valid) =>
        new RequestPasswordResetValidator().Validate(new RequestPasswordResetCommand(email)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("", "Parola123x", false)]          // belirteç yok
    [InlineData("tok", "kisa", false)]             // zayıf parola
    [InlineData("tok", "sadeceharfler", false)]    // rakam yok
    [InlineData("tok", "Parola123x", true)]
    public void Sifirlama_dogrulayicisi_belirteci_ve_parolayi_denetler(string token, string password, bool valid) =>
        new ResetPasswordValidator().Validate(new ResetPasswordCommand(token, password)).IsValid.Should().Be(valid);

    // ---------- E-posta şablonları ----------

    [Fact]
    public void Sifirlama_epostasi_baglanti_omur_ve_uyari_icerir_parola_icermez()
    {
        var content = PasswordEmails.ResetRequested("Ayşe Yılmaz", "http://x/sifre-sifirla?token=T0K3N", TimeSpan.FromMinutes(30));

        content.TemplateKey.Should().Be("password-reset-requested");
        content.Body.Should().Contain("Ayşe Yılmaz").And.Contain("http://x/sifre-sifirla?token=T0K3N").And.Contain("30 dakika")
            .And.Contain("yalnızca bir kez").And.Contain("paylaşmayın").And.Contain("yok sayabilirsiniz");
    }

    [Fact]
    public void Bildirim_epostasi_oturumlarin_kapandigini_soyler_baglanti_icermez()
    {
        var content = PasswordEmails.PasswordChanged("Ayşe Yılmaz");

        content.TemplateKey.Should().Be("password-changed");
        content.Body.Should().Contain("oturumlarınız kapatıldı").And.NotContain("http");
    }

    [Fact]
    public void Ayni_belirtec_ayni_tur_icin_ayni_tekrar_onleme_anahtarini_uretir_farkli_tur_farkli()
    {
        var id = Guid.NewGuid();
        var content = PasswordEmails.PasswordChanged("A");

        var a = PasswordEmails.ToMessage(id, "changed", "a@b.test", "A", content, Now);
        var b = PasswordEmails.ToMessage(id, "changed", "a@b.test", "A", content, Now);
        var c = PasswordEmails.ToMessage(id, "reset", "a@b.test", "A", content, Now);

        a.DedupKey.Should().Be(b.DedupKey).And.NotBe(c.DedupKey);
    }
}
