using AwesomeAssertions;
using Hospital.Application.Features.Auth;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Domain;

public class RefreshTokenAndJwtSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_gecerlilik_suresi_ve_baslangic_durumunu_ayarlar()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "ozet", Now, TimeSpan.FromDays(7));

        token.ExpiresAt.Should().Be(Now.AddDays(7));
        token.IsActive(Now).Should().BeTrue();
        token.IsActive(Now.AddDays(7)).Should().BeFalse(); // süre tam dolduğunda geçersiz
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Revoke_idempotenttir_ilk_iptal_zamani_ve_yerine_gecen_korunur()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "ozet", Now, TimeSpan.FromDays(7));
        var replacement = Guid.NewGuid();

        token.Revoke(Now.AddMinutes(1), replacement);
        token.Revoke(Now.AddMinutes(9), Guid.NewGuid());

        token.RevokedAt.Should().Be(Now.AddMinutes(1));
        token.ReplacedByTokenId.Should().Be(replacement);
        token.IsActive(Now.AddMinutes(2)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_pozitif_olmayan_omur_DomainException_firlatir(int days)
    {
        var act = () => RefreshToken.Issue(Guid.NewGuid(), "ozet", Now, TimeSpan.FromDays(days));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Issue_bos_kullanici_veya_ozet_DomainException_firlatir()
    {
        ((Action)(() => RefreshToken.Issue(Guid.Empty, "ozet", Now, TimeSpan.FromDays(1)))).Should().Throw<DomainException>();
        ((Action)(() => RefreshToken.Issue(Guid.NewGuid(), " ", Now, TimeSpan.FromDays(1)))).Should().Throw<DomainException>();
    }

    [Fact]
    public void RefreshTokenSecret_benzersiz_url_guvenli_ve_yeterince_uzun_deger_uretir()
    {
        var values = Enumerable.Range(0, 200).Select(_ => RefreshTokenSecret.Generate()).ToList();

        values.Should().OnlyHaveUniqueItems();
        values.Should().OnlyContain(v => v.Length >= 43 && v.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'));
    }

    [Fact]
    public void RefreshTokenSecret_Hash_deterministik_64_hane_hex_ve_degerden_farklidir()
    {
        var value = RefreshTokenSecret.Generate();

        var hash = RefreshTokenSecret.Hash(value);

        hash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        hash.Should().Be(RefreshTokenSecret.Hash(value));
        hash.Should().NotBe(RefreshTokenSecret.Hash(value + "x"));
        hash.Should().NotContain(value);
    }

    [Fact]
    public void JwtSettings_gecerli_ayarlari_kabul_eder()
    {
        var settings = new JwtSettings("iss", "aud", new string('k', 32));

        settings.Validated().Should().Be(settings);
    }

    [Theory]
    [InlineData("", "aud", "0123456789abcdef0123456789abcdef")]  // issuer yok
    [InlineData("iss", "", "0123456789abcdef0123456789abcdef")]  // audience yok
    [InlineData("iss", "aud", "")]                                // anahtar yok
    [InlineData("iss", "aud", "kisa-anahtar")]                    // 32 bayttan kısa
    public void JwtSettings_eksik_veya_zayif_ayar_InvalidOperationException_firlatir(string issuer, string audience, string key)
    {
        var act = () => new JwtSettings(issuer, audience, key).Validated();

        act.Should().Throw<InvalidOperationException>();
    }
}
