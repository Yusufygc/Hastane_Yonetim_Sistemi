using AwesomeAssertions;
using Hospital.Application.Features.Doctors.Commands.UpdateMyAvailability;

namespace Hospital.UnitTests.Application;

public class UpdateMyAvailabilityValidatorTests
{
    private readonly UpdateMyAvailabilityValidator _validator = new();

    private static UpdateMyAvailabilityCommand Valid() =>
        new(Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    [Fact]
    public void Gecerli_komut_dogrulamayi_gecer()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidTo_null_oldugunda_gecerlidir()
    {
        var result = _validator.Validate(Valid() with { ValidTo = null });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidTo_ValidFrom_ile_esit_oldugunda_gecerlidir()
    {
        var baseCommand = Valid();
        var result = _validator.Validate(baseCommand with { ValidTo = baseCommand.ValidFrom });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void EndTime_StartTime_bir_dakika_sonra_oldugunda_gecerlidir()
    {
        var baseCommand = Valid();
        var result = _validator.Validate(baseCommand with { EndTime = baseCommand.StartTime.AddMinutes(1) });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    public void Tum_haftanin_gunleri_gecerlidir(DayOfWeek day)
    {
        var result = _validator.Validate(Valid() with { Day = day });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Id_bos_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { Id = Guid.Empty });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.Id)).Subject;
        error.ErrorCode.Should().Be("NotEmptyValidator");
    }

    [Theory]
    [InlineData((DayOfWeek)7)]
    [InlineData((DayOfWeek)(-1))]
    public void Day_gecersiz_enum_degeri_oldugunda_reddedilir(DayOfWeek invalidDay)
    {
        var result = _validator.Validate(Valid() with { Day = invalidDay });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.Day)).Subject;
        error.ErrorMessage.Should().Be("Geçersiz gün (0 = Pazar … 6 = Cumartesi).");
    }

    [Fact]
    public void EndTime_StartTime_ile_esit_oldugunda_reddedilir()
    {
        var baseCommand = Valid();
        var result = _validator.Validate(baseCommand with { EndTime = baseCommand.StartTime });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.EndTime)).Subject;
        error.ErrorMessage.Should().Be("Bitiş saati başlangıç saatinden sonra olmalıdır.");
    }

    [Fact]
    public void EndTime_StartTimeden_kucuk_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(9, 0) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.EndTime)).Subject;
        error.ErrorMessage.Should().Be("Bitiş saati başlangıç saatinden sonra olmalıdır.");
    }

    [Fact]
    public void Gece_yarisi_EndTime_StartTimeden_kucuk_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { StartTime = new TimeOnly(23, 0), EndTime = new TimeOnly(0, 0) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.EndTime)).Subject;
        error.ErrorMessage.Should().Be("Bitiş saati başlangıç saatinden sonra olmalıdır.");
    }

    [Fact]
    public void ValidFrom_varsayilan_deger_oldugunda_reddedilir()
    {
        var result = _validator.Validate(Valid() with { ValidFrom = default, ValidTo = null });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.ValidFrom)).Subject;
        error.ErrorMessage.Should().Be("Geçerlilik başlangıcı zorunludur (yyyy-MM-dd).");
    }

    [Fact]
    public void ValidTo_ValidFromdan_once_oldugunda_reddedilir()
    {
        var baseCommand = Valid();
        var result = _validator.Validate(baseCommand with { ValidTo = baseCommand.ValidFrom.AddDays(-1) });

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateMyAvailabilityCommand.ValidTo)).Subject;
        error.ErrorMessage.Should().Be("Geçerlilik bitişi başlangıçtan önce olamaz.");
    }

    [Fact]
    public void Coklu_gecersiz_alan_oldugunda_tum_hatalar_doner()
    {
        var baseCommand = Valid();
        var command = baseCommand with
        {
            Id = Guid.Empty,
            EndTime = baseCommand.StartTime,
            ValidTo = baseCommand.ValidFrom.AddDays(-1)
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain(
            [nameof(UpdateMyAvailabilityCommand.Id), nameof(UpdateMyAvailabilityCommand.EndTime), nameof(UpdateMyAvailabilityCommand.ValidTo)]);
    }
}
