using AwesomeAssertions;
using Hospital.Application.Features.Appointments.Queries.GetDoctorSchedule;

namespace Hospital.UnitTests.Application;

public class GetDoctorScheduleValidatorTests
{
    private readonly GetDoctorScheduleValidator _validator = new();

    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ikisi_de_null_oldugunda_gecerlidir() =>
        _validator.Validate(new GetDoctorScheduleQuery(null, null)).IsValid.Should().BeTrue();

    [Fact]
    public void Yalniz_from_null_oldugunda_gecerlidir() =>
        _validator.Validate(new GetDoctorScheduleQuery(null, T0)).IsValid.Should().BeTrue();

    [Fact]
    public void Yalniz_to_null_oldugunda_gecerlidir() =>
        _validator.Validate(new GetDoctorScheduleQuery(T0, null)).IsValid.Should().BeTrue();

    [Fact]
    public void To_fromdan_buyuk_oldugunda_gecerlidir() =>
        _validator.Validate(new GetDoctorScheduleQuery(T0, T0.AddHours(1))).IsValid.Should().BeTrue();

    [Fact]
    public void To_ve_from_esit_oldugunda_gecerlidir() =>
        _validator.Validate(new GetDoctorScheduleQuery(T0, T0)).IsValid.Should().BeTrue();

    [Fact]
    public void Farkli_ofsetlerde_ayni_an_gecerlidir() =>
        _validator.Validate(new GetDoctorScheduleQuery(T0, T0.ToOffset(TimeSpan.FromHours(3)))).IsValid.Should().BeTrue();

    [Fact]
    public void To_fromdan_kucuk_oldugunda_reddedilir()
    {
        var result = _validator.Validate(new GetDoctorScheduleQuery(T0, T0.AddDays(-1)));

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(GetDoctorScheduleQuery.To));
        error.ErrorMessage.Should().Be("Bitiş (to) başlangıçtan (from) önce olamaz.");
    }

    [Fact]
    public void To_fromdan_bir_tick_once_oldugunda_reddedilir()
    {
        var result = _validator.Validate(new GetDoctorScheduleQuery(T0, T0.AddTicks(-1)));

        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(GetDoctorScheduleQuery.To));
        error.ErrorMessage.Should().Be("Bitiş (to) başlangıçtan (from) önce olamaz.");
    }
}
