using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.UnitTests.Domain;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid HospitalId = Guid.NewGuid();

    private static readonly ServiceSnapshot Service = new(Guid.NewGuid(), "Genel Muayene", 30, 500m, "TRY");

    private static Appointment NewAppointment(string? note = null, DateTimeOffset? start = null)
    {
        var s = start ?? Start;
        return Appointment.Request(PatientId, DoctorId, HospitalId, Service, s, s.AddMinutes(30), note, Now);
    }

    [Fact]
    public void Request_bekleyen_randevu_olusturur_zamanlari_UTC_ye_cevirir_ve_snapshot_alir()
    {
        var plus3 = TimeSpan.FromHours(3);
        var appointment = Appointment.Request(
            PatientId,
            DoctorId,
            HospitalId,
            Service,
            new DateTimeOffset(2026, 11, 2, 13, 0, 0, plus3),
            new DateTimeOffset(2026, 11, 2, 13, 30, 0, plus3),
            "  Baş ağrısı  ",
            Now);

        appointment.Status.Should().Be(AppointmentStatus.Pending);
        appointment.HoldsSlot.Should().BeTrue();
        appointment.StartAt.Should().Be(Start);
        appointment.StartAt.Offset.Should().Be(TimeSpan.Zero);
        appointment.EndAt.Offset.Should().Be(TimeSpan.Zero);
        appointment.Id.Should().NotBe(Guid.Empty);
        appointment.PatientNote.Should().Be("Baş ağrısı");
        appointment.ServiceNameSnapshot.Should().Be("Genel Muayene");
        appointment.DurationMinutesSnapshot.Should().Be(30);
        appointment.PriceSnapshot.Should().Be(500m);
        appointment.CurrencySnapshot.Should().Be("TRY");
        appointment.ServiceId.Should().Be(Service.ServiceId);
        appointment.HospitalId.Should().Be(HospitalId);
    }

    [Fact]
    public void Request_AppointmentRequestedEvent_uretir()
    {
        var appointment = NewAppointment();

        var requested = appointment.DomainEvents.Should().ContainSingle().Subject.Should().BeOfType<AppointmentRequestedEvent>().Subject;
        requested.AppointmentId.Should().Be(appointment.Id);
        requested.PatientId.Should().Be(PatientId);
        requested.DoctorId.Should().Be(DoctorId);
        requested.ServiceName.Should().Be("Genel Muayene");
    }

    [Theory]
    [InlineData(0)]   // bitiş = başlangıç
    [InlineData(-30)] // bitiş başlangıçtan önce
    public void Request_bitis_baslangictan_sonra_degilse_DomainException_firlatir(int minutes)
    {
        var act = () => Appointment.Request(PatientId, DoctorId, HospitalId, Service, Start, Start.AddMinutes(minutes), null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Request_bos_kimliklerle_DomainException_firlatir()
    {
        var emptyPatient = () => Appointment.Request(Guid.Empty, DoctorId, HospitalId, Service, Start, Start.AddMinutes(30), null, Now);
        var emptyHospital = () => Appointment.Request(PatientId, DoctorId, Guid.Empty, Service, Start, Start.AddMinutes(30), null, Now);
        var emptyService = () => Appointment.Request(
            PatientId, DoctorId, HospitalId, Service with { ServiceId = Guid.Empty }, Start, Start.AddMinutes(30), null, Now);

        emptyPatient.Should().Throw<DomainException>();
        emptyHospital.Should().Throw<DomainException>();
        emptyService.Should().Throw<DomainException>();
    }

    [Fact]
    public void Request_cok_uzun_not_reddedilir()
    {
        var act = () => NewAppointment(note: new string('x', Appointment.NoteMaxLength + 1));

        act.Should().Throw<DomainException>();
    }

    // ---------- Durum geçişleri ----------

    [Fact]
    public void Approve_durumu_onayliya_cevirir_olay_uretir_ve_isleyeni_kaydeder()
    {
        var appointment = NewAppointment();

        appointment.Approve(DoctorId, Now.AddHours(1));

        appointment.Status.Should().Be(AppointmentStatus.Approved);
        appointment.HoldsSlot.Should().BeTrue();
        appointment.StatusChangedBy.Should().Be(DoctorId);
        appointment.UpdatedAt.Should().Be(Now.AddHours(1));
        appointment.DomainEvents.Should().ContainSingle(e => e is AppointmentApprovedEvent);
    }

    [Fact]
    public void Reject_nedeni_kaydeder_slotu_serbest_birakir_ve_olay_uretir()
    {
        var appointment = NewAppointment();

        appointment.Reject(DoctorId, "  Doktor o gün yok  ", Now);

        appointment.Status.Should().Be(AppointmentStatus.Rejected);
        appointment.HoldsSlot.Should().BeFalse();
        appointment.DecisionReason.Should().Be("Doktor o gün yok");
        appointment.DomainEvents.OfType<AppointmentRejectedEvent>().Should().ContainSingle()
            .Which.Reason.Should().Be("Doktor o gün yok");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_ve_Cancel_nedensiz_DomainException_firlatir(string reason)
    {
        ((Action)(() => NewAppointment().Reject(DoctorId, reason, Now))).Should().Throw<DomainException>();
        ((Action)(() => NewAppointment().Cancel(PatientId, reason, Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_bekleyen_ve_onayli_randevuyu_iptal_eder()
    {
        var pending = NewAppointment();
        var approved = NewAppointment();
        approved.Approve(DoctorId, Now);

        pending.Cancel(PatientId, "Vazgeçtim", Now);
        approved.Cancel(DoctorId, "Acil durum", Now);

        pending.Status.Should().Be(AppointmentStatus.Cancelled);
        approved.Status.Should().Be(AppointmentStatus.Cancelled);
        approved.HoldsSlot.Should().BeFalse();
        approved.DomainEvents.OfType<AppointmentCancelledEvent>().Should().ContainSingle()
            .Which.CancelledBy.Should().Be(DoctorId);
    }

    [Fact]
    public void Complete_yalnizca_onayli_ve_baslamis_randevuyu_tamamlar()
    {
        var appointment = NewAppointment();
        appointment.Approve(DoctorId, Now);

        var beforeStart = () => appointment.Complete(DoctorId, Start.AddMinutes(-1));
        beforeStart.Should().Throw<DomainException>();

        appointment.Complete(DoctorId, Start);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.HoldsSlot.Should().BeFalse();
    }

    [Fact]
    public void Gecersiz_gecis_DomainException_firlatir_ve_durumu_degistirmez()
    {
        var appointment = NewAppointment();
        appointment.Reject(DoctorId, "Uygun değil", Now);

        ((Action)(() => appointment.Approve(DoctorId, Now))).Should().Throw<DomainException>();
        ((Action)(() => appointment.Cancel(PatientId, "x", Now))).Should().Throw<DomainException>();
        ((Action)(() => appointment.Complete(DoctorId, Start.AddDays(1)))).Should().Throw<DomainException>();
        appointment.Status.Should().Be(AppointmentStatus.Rejected);
    }

    [Fact]
    public void Islemi_yapan_bos_kimlik_reddedilir()
    {
        var act = () => NewAppointment().Approve(Guid.Empty, Now);

        act.Should().Throw<DomainException>();
    }

    // ---------- Zaman aşımı (sistem işlemi) ----------

    [Fact]
    public void ExpireWithoutResponse_bekleyen_talebi_reddeder_islemci_bos_neden_sabit_olay_uretir()
    {
        var appointment = NewAppointment();

        appointment.ExpireWithoutResponse(Now.AddDays(3));

        appointment.Status.Should().Be(AppointmentStatus.Rejected);
        appointment.HoldsSlot.Should().BeFalse();
        appointment.StatusChangedBy.Should().BeNull(); // sistem işlemi: kullanıcı yok
        appointment.DecisionReason.Should().Be(Appointment.ExpiredReason);
        appointment.UpdatedAt.Should().Be(Now.AddDays(3));
        appointment.DomainEvents.OfType<AppointmentRejectedEvent>().Should().ContainSingle()
            .Which.Reason.Should().Be(Appointment.ExpiredReason);
    }

    [Fact]
    public void ExpireWithoutResponse_yalniz_bekleyen_talebe_uygulanir()
    {
        var approved = NewAppointment();
        approved.Approve(DoctorId, Now);
        var cancelled = NewAppointment();
        cancelled.Cancel(PatientId, "Vazgeçtim", Now);

        ((Action)(() => approved.ExpireWithoutResponse(Now))).Should().Throw<DomainException>();
        ((Action)(() => cancelled.ExpireWithoutResponse(Now))).Should().Throw<DomainException>();
        approved.Status.Should().Be(AppointmentStatus.Approved);
    }

    // ---------- Hasta iptal süre sınırı ----------

    [Theory]
    [InlineData(3, false)] // 3 saat kala, sınır 2 saat → iptal edilebilir
    [InlineData(2, false)] // tam 2 saat kala: kural "2 saatten az kala" → hâlâ iptal edilebilir
    [InlineData(1, true)]  // 1 saat kala → sınır içinde, hasta iptal edemez
    [InlineData(0, true)]
    public void Onayli_randevuda_hasta_iptal_sure_siniri(int hoursLeft, bool expectedWithin)
    {
        var appointment = NewAppointment();
        appointment.Approve(DoctorId, Now);

        var within = appointment.IsWithinCancellationDeadline(Start.AddHours(-hoursLeft), TimeSpan.FromHours(2));

        within.Should().Be(expectedWithin);
    }

    [Fact]
    public void Bekleyen_randevu_sureye_bakilmaksizin_iptal_edilebilir()
    {
        var appointment = NewAppointment();

        appointment.IsWithinCancellationDeadline(Start.AddMinutes(-5), TimeSpan.FromHours(2)).Should().BeFalse();
    }
}
