using AwesomeAssertions;
using Hospital.Application.Features.Hospitals.Commands.CreateHospital;
using Hospital.Domain.Entities.Facility;

namespace Hospital.UnitTests.Application;

public class CreateHospitalValidatorTests
{
    private readonly CreateHospitalValidator _validator = new();

    private static CreateHospitalCommand Valid() =>
        new("Merkez Hastanesi", "Atatürk Bulvarı No:123", "+902121234567", "Europe/Istanbul");

    [Fact]
    public void Gecerli_komut_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Gecerli_maksimum_uzunluktaki_isim_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid() with { Name = new string('a', HospitalBranch.NameMaxLength) });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Gecerli_tek_karakterlik_isim_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid() with { Name = "A" });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Address_null_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Address = null });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Address_bos_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Address = string.Empty });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Gecerli_maksimum_uzunluktaki_adres_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid() with { Address = new string('a', HospitalBranch.AddressMaxLength) });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Phone_null_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Phone = null });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Phone_bos_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Phone = string.Empty });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Gecerli_maksimum_uzunluktaki_telefon_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid() with { Phone = new string('1', HospitalBranch.PhoneMaxLength) });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    public void Gecerli_saat_dilimi_dogrulamayi_gecer(string timeZoneId)
    {
        var result = _validator.Validate(Valid() with { TimeZoneId = timeZoneId });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Isim_bos_veya_bosluk_veya_null_oldugunda_reddedilir(string? name)
    {
        var result = _validator.Validate(Valid() with { Name = name! });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateHospitalCommand.Name)).Subject;
        error.ErrorMessage.Should().Be("Şube adı zorunludur.");
    }

    [Fact]
    public void Isim_maksimum_uzunlugu_astiginda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Name = new string('a', HospitalBranch.NameMaxLength + 1) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateHospitalCommand.Name)).Subject;
        error.ErrorCode.Should().Be("MaximumLengthValidator");
    }

    [Fact]
    public void Adres_maksimum_uzunlugu_astiginda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Address = new string('a', HospitalBranch.AddressMaxLength + 1) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateHospitalCommand.Address)).Subject;
        error.ErrorCode.Should().Be("MaximumLengthValidator");
    }

    [Fact]
    public void Telefon_maksimum_uzunlugu_astiginda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Phone = new string('1', HospitalBranch.PhoneMaxLength + 1) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateHospitalCommand.Phone)).Subject;
        error.ErrorCode.Should().Be("MaximumLengthValidator");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Saat_dilimi_bos_veya_bosluk_veya_null_oldugunda_reddedilir(string? timeZoneId)
    {
        var result = _validator.Validate(Valid() with { TimeZoneId = timeZoneId! });

        result.IsValid.Should().BeFalse();
        // NotEmpty ve Must kuralları Cascade olmadan birlikte tetiklenir.
        var messages = result.Errors
            .Where(e => e.PropertyName == nameof(CreateHospitalCommand.TimeZoneId))
            .Select(e => e.ErrorMessage)
            .ToList();
        messages.Should().Contain("Saat dilimi zorunludur.");
        messages.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("Gecersiz/SaatDilimi")]
    [InlineData("Mars/Olympus")]
    [InlineData("InvalidZone")]
    public void Saat_dilimi_bilinmeyen_deger_oldugunda_reddedilir(string invalidTimeZone)
    {
        var result = _validator.Validate(Valid() with { TimeZoneId = invalidTimeZone });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateHospitalCommand.TimeZoneId)).Subject;
        error.ErrorMessage.Should().Be("Geçersiz saat dilimi (IANA kimliği bekleniyor, ör. Europe/Istanbul).");
    }

    [Fact]
    public void Coklu_gecersiz_alan_oldugunda_tum_hatalar_doner()
    {
        var command = new CreateHospitalCommand(
            Name: "",
            Address: new string('a', HospitalBranch.AddressMaxLength + 1),
            Phone: new string('1', HospitalBranch.PhoneMaxLength + 1),
            TimeZoneId: "Gecersiz/SaatDilimi");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain([
            nameof(CreateHospitalCommand.Name),
            nameof(CreateHospitalCommand.Address),
            nameof(CreateHospitalCommand.Phone),
            nameof(CreateHospitalCommand.TimeZoneId)
        ]);
    }
}
