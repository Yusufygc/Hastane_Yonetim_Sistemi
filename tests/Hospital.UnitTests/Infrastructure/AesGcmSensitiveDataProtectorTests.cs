using System.Security.Cryptography;
using AwesomeAssertions;
using Hospital.Application.Security;
using Hospital.Infrastructure.Security;

namespace Hospital.UnitTests.Infrastructure;

public class AesGcmSensitiveDataProtectorTests
{
    private const string Purpose = "patient.identity_number";

    private static string NewKey(byte fill) => Convert.ToBase64String(Enumerable.Repeat(fill, 32).ToArray());

    private static AesGcmSensitiveDataProtector Create(byte fill = 1) => new(new DataProtectionSettings(NewKey(fill)));

    [Fact]
    public void Sifrele_coz_gidis_donus_ve_onek()
    {
        var protector = Create();

        var stored = protector.Protect("10000000146", Purpose);

        stored.Should().StartWith("enc:v1:").And.NotContain("10000000146");
        stored.Length.Should().BeLessThanOrEqualTo(100); // patient.identity_number sütunu varchar(100)
        protector.Unprotect(stored, Purpose).Should().Be("10000000146");
    }

    [Fact]
    public void Ayni_veri_her_seferinde_farkli_sifreli_metin_uretir_rastgele_nonce()
    {
        var protector = Create();

        var first = protector.Protect("10000000146", Purpose);
        var second = protector.Protect("10000000146", Purpose);

        first.Should().NotBe(second);
        protector.Unprotect(first, Purpose).Should().Be(protector.Unprotect(second, Purpose));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Türkçe karakterler: ğüşiöçİ — ✓")]
    public void Her_metin_bozulmadan_geri_cozulur(string value)
    {
        var protector = Create();

        protector.Unprotect(protector.Protect(value, Purpose), Purpose).Should().Be(value);
    }

    [Fact]
    public void Degistirilmis_sifreli_veri_cozulemez()
    {
        var protector = Create();
        var stored = protector.Protect("10000000146", Purpose);
        var payload = Convert.FromBase64String(stored["enc:v1:".Length..]);
        payload[^1] ^= 0x01; // son bit çevrilir

        var act = () => protector.Unprotect("enc:v1:" + Convert.ToBase64String(payload), Purpose);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Yanlis_amac_etiketiyle_cozulemez_alan_degisimi_engellenir()
    {
        var protector = Create();
        var stored = protector.Protect("10000000146", Purpose);

        var act = () => protector.Unprotect(stored, "doctor.phone");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Baska_anahtarla_cozulemez()
    {
        var stored = Create(1).Protect("10000000146", Purpose);

        var act = () => Create(2).Unprotect(stored, Purpose);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Kisa_veya_bozuk_sifreli_veri_acik_hata_verir()
    {
        var protector = Create();

        ((Action)(() => protector.Unprotect("enc:v1:" + Convert.ToBase64String(new byte[5]), Purpose))).Should().Throw<CryptographicException>();
        ((Action)(() => protector.Unprotect("enc:v1:%%bozuk%%", Purpose))).Should().Throw<FormatException>();
    }

    [Fact]
    public void Onekli_olmayan_eski_duz_metin_oldugu_gibi_doner_yukseltme_icin()
    {
        Create().Unprotect("10000000146", Purpose).Should().Be("10000000146");
    }

    [Fact]
    public void Kor_indeks_deterministik_64_onaltilik_amaca_ve_anahtara_ozel()
    {
        var protector = Create(1);

        var hash = protector.BlindIndex("10000000146", Purpose);

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
        protector.BlindIndex("10000000146", Purpose).Should().Be(hash);
        protector.BlindIndex("10000000147", Purpose).Should().NotBe(hash);
        protector.BlindIndex("10000000146", "doctor.phone").Should().NotBe(hash);
        Create(2).BlindIndex("10000000146", Purpose).Should().NotBe(hash);
        hash.Should().NotContain("10000000146");
    }

    [Fact]
    public void Eksik_bozuk_veya_yanlis_uzunluktaki_anahtar_baslangicta_reddedilir()
    {
        ((Action)(() => new AesGcmSensitiveDataProtector(new DataProtectionSettings("")))).Should().Throw<InvalidOperationException>().WithMessage("*zorunludur*");
        ((Action)(() => new AesGcmSensitiveDataProtector(new DataProtectionSettings("base64-degil!!")))).Should().Throw<InvalidOperationException>().WithMessage("*Base64*");
        ((Action)(() => new AesGcmSensitiveDataProtector(new DataProtectionSettings(Convert.ToBase64String(new byte[16]))))).Should().Throw<InvalidOperationException>().WithMessage("*32 bayt*");
        ((Action)(() => new AesGcmSensitiveDataProtector(new DataProtectionSettings(Convert.ToBase64String(new byte[33]))))).Should().Throw<InvalidOperationException>().WithMessage("*32 bayt*");
    }
}
