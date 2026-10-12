using AwesomeAssertions;
using Hospital.Domain.Users;

namespace Hospital.UnitTests.Domain;

public class TurkishIdentityNumberTests
{
    [Theory]
    [InlineData("10000000146")]
    public void Gecerli_numaralar_kabul_edilir(string value) =>
        TurkishIdentityNumber.IsValid(value).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890")]       // 10 hane
    [InlineData("123456789012")]     // 12 hane
    [InlineData("0000000014")]
    [InlineData("01000000146")]      // 0 ile başlar
    [InlineData("1000000014a")]      // rakam değil
    [InlineData("10000000147")]      // son basamak yanlış
    [InlineData("10000000156")]      // 10. basamak yanlış
    public void Gecersiz_numaralar_reddedilir(string? value) =>
        TurkishIdentityNumber.IsValid(value).Should().BeFalse();
}
