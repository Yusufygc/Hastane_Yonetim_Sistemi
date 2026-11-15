using AwesomeAssertions;
using Hospital.Application.Features.Services.Commands.CreateService;
using Hospital.Domain.Entities.Facility;

namespace Hospital.UnitTests.Application;

public class CreateServiceValidatorTests
{
    private readonly CreateServiceValidator _validator = new();

    private static CreateServiceCommand Valid() =>
        new(
            HospitalId: Guid.NewGuid(),
            DepartmentId: Guid.NewGuid(),
            Name: "Genel Muayene",
            DurationMinutes: 30,
            Price: 500.00m,
            Currency: "TRY");

    [Fact]
    public void Gecerli_komut_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DepartmentId_null_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { DepartmentId = null });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Name_maksimum_uzunlukta_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Name = new string('a', Service.NameMaxLength) });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Name_tek_karakter_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Name = "a" });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DurationMinutes_alt_sinirda_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { DurationMinutes = 1 });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DurationMinutes_ust_sinirda_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { DurationMinutes = Service.MaxDurationMinutes });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Price_sifir_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Price = 0m });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Price_maksimum_degerde_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { Price = Service.MaxPrice });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void HospitalId_bos_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { HospitalId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.HospitalId)).Subject;
        error.ErrorCode.Should().Be("NotEmptyValidator");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Name_bos_veya_bosluk_veya_null_oldugunda_reddedilir(string? name)
    {
        var result = _validator.Validate(Valid() with { Name = name! });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.Name)).Subject;
        error.ErrorMessage.Should().Be("Hizmet adı zorunludur.");
    }

    [Fact]
    public void Name_maksimum_uzunlugu_astiginda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Name = new string('a', Service.NameMaxLength + 1) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.Name)).Subject;
        error.ErrorCode.Should().Be("MaximumLengthValidator");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DurationMinutes_birden_kucuk_oldugunda_reddedilir(int duration)
    {
        var result = _validator.Validate(Valid() with { DurationMinutes = duration });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.DurationMinutes)).Subject;
        error.ErrorMessage.Should().Be($"Hizmet süresi 1 ile {Service.MaxDurationMinutes} dakika arasında olmalıdır.");
    }

    [Fact]
    public void DurationMinutes_maksimum_degeri_astiginda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { DurationMinutes = Service.MaxDurationMinutes + 1 });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.DurationMinutes)).Subject;
        error.ErrorMessage.Should().Be($"Hizmet süresi 1 ile {Service.MaxDurationMinutes} dakika arasında olmalıdır.");
    }

    [Fact]
    public void Price_sifirdan_kucuk_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Price = -0.01m });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.Price)).Subject;
        error.ErrorMessage.Should().Be("Ücret 0'dan küçük olamaz.");
    }

    [Fact]
    public void Price_maksimum_degeri_astiginda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Price = Service.MaxPrice + 1m });

        result.IsValid.Should().BeFalse();
        // MaxPrice'ı aşan her değer numeric(12,2) hassasiyetini de aştığı için iki kural birlikte tetiklenir.
        var messages = result.Errors
            .Where(e => e.PropertyName == nameof(CreateServiceCommand.Price))
            .Select(e => e.ErrorMessage)
            .ToList();
        messages.Should().HaveCount(2);
        messages.Should().Contain("Ücret çok büyük.");
        messages.Should().Contain("Ücret en fazla 2 ondalık basamak içerebilir.");
    }

    [Fact]
    public void Price_ikiden_fazla_ondalik_basamak_icerdiginde_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Price = 10.999m });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.Price)).Subject;
        error.ErrorMessage.Should().Be("Ücret en fazla 2 ondalık basamak içerebilir.");
    }

    [Fact]
    public void Currency_bos_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Currency = "" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateServiceCommand.Currency) &&
            e.ErrorMessage == "Para birimi zorunludur.");
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("TRYX")]
    public void Currency_uc_karakterden_farkli_uzunlukta_oldugunda_reddedilir(string currency)
    {
        var result = _validator.Validate(Valid() with { Currency = currency });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.Currency)).Subject;
        error.ErrorMessage.Should().Be("Para birimi 3 harfli ISO kodu olmalıdır (ör. TRY).");
    }

    [Theory]
    [InlineData("TR1")]
    [InlineData("123")]
    [InlineData("TR-")]
    public void Currency_harf_disi_karakter_icerdiginde_reddedilir(string currency)
    {
        var result = _validator.Validate(Valid() with { Currency = currency });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateServiceCommand.Currency)).Subject;
        error.ErrorMessage.Should().Be("Para birimi yalnızca harf içermelidir.");
    }

    [Fact]
    public void Coklu_gecersiz_alan_oldugunda_tum_hatalar_doner()
    {
        var command = Valid() with
        {
            HospitalId = Guid.Empty,
            Name = "",
            DurationMinutes = 0,
            Price = -5m,
            Currency = "12"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain([
            nameof(CreateServiceCommand.HospitalId),
            nameof(CreateServiceCommand.Name),
            nameof(CreateServiceCommand.DurationMinutes),
            nameof(CreateServiceCommand.Price),
            nameof(CreateServiceCommand.Currency)
        ]);
    }
}
