using AwesomeAssertions;
using Hospital.Application.Features.Appointments.Commands.CancelAppointment;
using Hospital.Application.Features.Appointments.Commands.CreateAppointment;
using Hospital.Application.Features.Appointments.Commands.RejectAppointment;
using Hospital.Application.Features.Appointments.Queries.GetAvailableSlots;
using Hospital.Application.Features.Appointments.Queries.GetDoctorAppointments;
using Hospital.Application.Features.Appointments.Queries.GetMyAppointments;
using Hospital.Application.Features.Appointments.Queries.ListAppointments;

namespace Hospital.UnitTests.Application;

public class Faz4ValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Gecerli_randevu_komutu_dogrulamayi_gecer() =>
        new CreateAppointmentValidator().Validate(new CreateAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), Start, "Not")).IsValid
            .Should().BeTrue();

    [Fact]
    public void Randevu_komutunda_bos_alanlar_ve_uzun_not_alan_bazli_reddedilir()
    {
        var result = new CreateAppointmentValidator().Validate(
            new CreateAppointmentCommand(Guid.Empty, Guid.Empty, default, new string('x', 501)));

        result.Errors.Select(e => e.PropertyName).Should().Contain(
            [nameof(CreateAppointmentCommand.DoctorId), nameof(CreateAppointmentCommand.ServiceId),
             nameof(CreateAppointmentCommand.StartAt), nameof(CreateAppointmentCommand.PatientNote)]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Red_ve_iptal_nedeni_zorunludur(string reason)
    {
        new RejectAppointmentValidator().Validate(new RejectAppointmentCommand(Guid.NewGuid(), reason)).Errors
            .Should().Contain(e => e.PropertyName == nameof(RejectAppointmentCommand.Reason));
        new CancelAppointmentValidator().Validate(new CancelAppointmentCommand(Guid.NewGuid(), reason)).Errors
            .Should().Contain(e => e.PropertyName == nameof(CancelAppointmentCommand.Reason));
    }

    [Fact]
    public void Gecerli_neden_ve_uzunluk_siniri()
    {
        new RejectAppointmentValidator().Validate(new RejectAppointmentCommand(Guid.NewGuid(), "Doktor müsait değil")).IsValid.Should().BeTrue();
        new CancelAppointmentValidator().Validate(new CancelAppointmentCommand(Guid.NewGuid(), new string('x', 501))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Slot_sorgusunda_zorunlu_alanlar_denetlenir()
    {
        var validator = new GetAvailableSlotsValidator();

        validator.Validate(new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 2))).IsValid.Should().BeTrue();
        validator.Validate(new GetAvailableSlotsQuery(Guid.Empty, Guid.NewGuid(), new DateOnly(2026, 11, 2))).IsValid.Should().BeFalse();
        validator.Validate(new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.Empty, new DateOnly(2026, 11, 2))).IsValid.Should().BeFalse();
        validator.Validate(new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.NewGuid(), default)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("pending", true)]
    [InlineData("COMPLETED", true)]
    [InlineData("bekliyor", false)]
    public void Liste_sorgularinda_durum_filtresi_dogrulanir(string? status, bool valid)
    {
        new GetMyAppointmentsValidator().Validate(new GetMyAppointmentsQuery(status)).IsValid.Should().Be(valid);
        new GetDoctorAppointmentsValidator().Validate(new GetDoctorAppointmentsQuery(null, null, status)).IsValid.Should().Be(valid);
        new ListAppointmentsValidator().Validate(new ListAppointmentsQuery(null, status, null, null)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Liste_sorgularinda_bitis_baslangictan_once_olamaz()
    {
        new GetDoctorAppointmentsValidator().Validate(new GetDoctorAppointmentsQuery(Start, Start.AddDays(-1), null)).IsValid
            .Should().BeFalse();
        new ListAppointmentsValidator().Validate(new ListAppointmentsQuery(null, null, Start, Start.AddDays(-1))).IsValid
            .Should().BeFalse();
        new ListAppointmentsValidator().Validate(new ListAppointmentsQuery(null, null, Start, Start)).IsValid
            .Should().BeTrue();
        new ListAppointmentsValidator().Validate(new ListAppointmentsQuery(null, null, null, Start)).IsValid
            .Should().BeTrue();
    }
}
