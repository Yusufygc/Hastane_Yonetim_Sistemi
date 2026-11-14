using AwesomeAssertions;
using Hospital.Application.Features.Departments.Commands.CreateDepartment;
using Hospital.Domain.Entities.Facility;

namespace Hospital.UnitTests.Application;

public class CreateDepartmentValidatorTests
{
    private readonly CreateDepartmentValidator _validator = new();
    private static readonly Guid HospitalId = Guid.NewGuid();

    [Fact]
    public void Gecerli_komut_dogrulamayi_gecer()
    {
        var command = new CreateDepartmentCommand(HospitalId, "Kardiyoloji");
        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Gecerli_maksimum_uzunluktaki_isim_dogrulamayi_gecer()
    {
        var command = new CreateDepartmentCommand(HospitalId, new string('a', Department.NameMaxLength));
        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Gecerli_tek_karakterlik_isim_dogrulamayi_gecer()
    {
        var command = new CreateDepartmentCommand(HospitalId, "a");
        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void HospitalId_bos_oldugunda_reddedilir()
    {
        var command = new CreateDepartmentCommand(Guid.Empty, "Kardiyoloji");
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(CreateDepartmentCommand.HospitalId));
        error.ErrorCode.Should().Be("NotEmptyValidator");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Isim_bos_veya_bosluk_veya_null_oldugunda_reddedilir(string? name)
    {
        var command = new CreateDepartmentCommand(HospitalId, name!);
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(CreateDepartmentCommand.Name));
        error.ErrorMessage.Should().Be("Bölüm adı zorunludur.");
    }

    [Fact]
    public void Isim_maksimum_uzunlugu_astiginda_reddedilir()
    {
        var command = new CreateDepartmentCommand(HospitalId, new string('a', Department.NameMaxLength + 1));
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(CreateDepartmentCommand.Name));
        error.ErrorCode.Should().Be("MaximumLengthValidator");
    }

    [Fact]
    public void HospitalId_ve_isim_ikisi_de_gecersiz_oldugunda_her_iki_hata_da_doner()
    {
        var command = new CreateDepartmentCommand(Guid.Empty, "");
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should()
            .Contain([nameof(CreateDepartmentCommand.HospitalId), nameof(CreateDepartmentCommand.Name)]);
    }
}
