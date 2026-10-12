using AwesomeAssertions;
using Hospital.Application.Features.Auth.Commands.RegisterPatient;

namespace Hospital.UnitTests.Application;

public class RegisterPatientValidatorTests
{
    private readonly RegisterPatientValidator _validator = new();

    private static RegisterPatientCommand Valid() => new(
        "hasta@example.test", "Test1234x", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 5, 17), null);

    [Fact]
    public void Gecerli_komut_dogrulamayi_gecer() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("kisa1")]            // 8 karakterden kısa
    [InlineData("sadeceharflerr")]   // rakam yok
    [InlineData("1234567890")]       // harf yok
    public void Zayif_parola_reddedilir(string password)
    {
        var result = _validator.Validate(Valid() with { Password = password });

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterPatientCommand.Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("eposta-degil")]
    public void Gecersiz_eposta_reddedilir(string email)
    {
        var result = _validator.Validate(Valid() with { Email = email });

        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterPatientCommand.Email));
    }

    [Fact]
    public void Gecersiz_TC_ve_gelecek_dogum_tarihi_reddedilir()
    {
        var result = _validator.Validate(Valid() with
        {
            IdentityNumber = "12345678901",
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
        });

        result.Errors.Select(e => e.PropertyName).Should()
            .Contain([nameof(RegisterPatientCommand.IdentityNumber), nameof(RegisterPatientCommand.BirthDate)]);
    }
}
