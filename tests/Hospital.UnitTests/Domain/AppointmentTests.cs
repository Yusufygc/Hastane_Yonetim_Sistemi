using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.UnitTests.Domain;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Request_bekleyen_randevu_olusturur_ve_zamanlari_UTC_ye_cevirir()
    {
        var plus3 = TimeSpan.FromHours(3);
        var appointment = Appointment.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 2, 13, 0, 0, plus3),
            new DateTimeOffset(2026, 11, 2, 13, 30, 0, plus3),
            Now);

        appointment.Status.Should().Be(AppointmentStatus.Pending);
        appointment.StartAt.Should().Be(Start);
        appointment.StartAt.Offset.Should().Be(TimeSpan.Zero);
        appointment.EndAt.Offset.Should().Be(TimeSpan.Zero);
        appointment.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(0)]   // bitiş = başlangıç
    [InlineData(-30)] // bitiş başlangıçtan önce
    public void Request_bitis_baslangictan_sonra_degilse_DomainException_firlatir(int minutes)
    {
        var act = () => Appointment.Request(Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMinutes(minutes), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Request_bos_kimliklerle_DomainException_firlatir()
    {
        var act = () => Appointment.Request(Guid.Empty, Guid.NewGuid(), Start, Start.AddMinutes(30), Now);

        act.Should().Throw<DomainException>();
    }
}
