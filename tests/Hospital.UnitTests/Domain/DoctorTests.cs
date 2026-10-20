using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Domain;

public class DoctorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();

    private static Doctor NewDoctor() =>
        Doctor.Create(" Doktor@Example.Test ", "hash", " Ayşe ", " Yılmaz ", Guid.NewGuid(), " Dahiliye ", " D-1 ", " 0555 ", Now);

    /// <summary>Hafta içi 08:00-17:00, Cumartesi 09:00-13:00, Pazar kapalı.</summary>
    private static List<HospitalWorkingHours> HospitalHours()
    {
        var hospital = HospitalBranch.Create(Guid.NewGuid(), "Merkez", null, null, "Europe/Istanbul");
        hospital.SetWorkingHours(
        [
            new WorkingHoursInput(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
            new WorkingHoursInput(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
        ]);
        return [.. hospital.WorkingHours];
    }

    private static readonly DateOnly Today = new(2026, 10, 20);

    // ---------- Oluşturma ----------

    [Fact]
    public void Create_alanlari_normallestirir_ve_doktor_rolu_atar()
    {
        var doctor = NewDoctor();

        doctor.Email.Should().Be("doktor@example.test");
        doctor.FullName.Should().Be("Ayşe Yılmaz");
        doctor.Specialty.Should().Be("Dahiliye");
        doctor.DiplomaNo.Should().Be("D-1");
        doctor.Phone.Should().Be("0555");
        doctor.Role.Should().Be(UserRole.Doctor);
        doctor.IsActive.Should().BeTrue();
        doctor.ScopeClaims.Should().BeEmpty();
    }

    [Fact]
    public void Create_bos_zorunlu_alan_DomainException_firlatir()
    {
        var act = () => Doctor.Create("d@e.test", "hash", "Ayşe", " ", Guid.NewGuid(), "Dahiliye", "D-1", null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Doktor_kendi_profilinde_yalniz_uzmanlik_ve_telefonu_degistirir()
    {
        var doctor = NewDoctor();
        var departmentId = doctor.DepartmentId;

        doctor.UpdateOwnProfile("Kardiyoloji", null);

        doctor.Specialty.Should().Be("Kardiyoloji");
        doctor.Phone.Should().BeNull();
        doctor.FirstName.Should().Be("Ayşe");
        doctor.DiplomaNo.Should().Be("D-1");
        doctor.DepartmentId.Should().Be(departmentId);
    }

    // ---------- Hizmet talep/onay akışı ----------

    [Fact]
    public void Talep_bekleyen_durumda_olusur_ve_onaylaninca_onayli_olur()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();

        var link = doctor.RequestService(serviceId, doctor.Id, Now);
        link.Status.Should().Be(DoctorServiceStatus.Pending);
        link.DecidedAt.Should().BeNull();

        doctor.ApproveService(serviceId, AdminId, Now.AddHours(1));

        link.Status.Should().Be(DoctorServiceStatus.Approved);
        link.DecidedBy.Should().Be(AdminId);
        link.DecidedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Ayni_hizmet_icin_bekleyen_veya_onayli_talep_tekrarlanamaz()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();
        doctor.RequestService(serviceId, doctor.Id, Now);

        var whilePending = () => doctor.RequestService(serviceId, doctor.Id, Now);
        doctor.ApproveService(serviceId, AdminId, Now);
        var whileApproved = () => doctor.RequestService(serviceId, doctor.Id, Now);

        whilePending.Should().Throw<DomainException>();
        whileApproved.Should().Throw<DomainException>();
        doctor.Services.Should().ContainSingle();
    }

    [Fact]
    public void Reddedilen_talep_yeniden_talep_edilince_bekleyene_doner()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();
        doctor.RequestService(serviceId, doctor.Id, Now);
        doctor.RejectService(serviceId, AdminId, Now);

        var link = doctor.RequestService(serviceId, doctor.Id, Now.AddDays(1));

        link.Status.Should().Be(DoctorServiceStatus.Pending);
        link.DecidedBy.Should().BeNull();
        link.DecidedAt.Should().BeNull();
        doctor.Services.Should().ContainSingle();
    }

    [Fact]
    public void Yalniz_bekleyen_talep_reddedilebilir()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();
        doctor.RequestService(serviceId, doctor.Id, Now);
        doctor.ApproveService(serviceId, AdminId, Now);

        var act = () => doctor.RejectService(serviceId, AdminId, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Olmayan_talep_onaylanamaz_ve_reddedilemez()
    {
        var doctor = NewDoctor();

        ((Action)(() => doctor.ApproveService(Guid.NewGuid(), AdminId, Now))).Should().Throw<DomainException>();
        ((Action)(() => doctor.RejectService(Guid.NewGuid(), AdminId, Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Yonetici_atamasi_onay_adimi_olmadan_onayli_olur_ve_idempotenttir()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();

        doctor.AssignService(serviceId, AdminId, Now);
        doctor.AssignService(serviceId, AdminId, Now.AddHours(1));

        var link = doctor.Services.Should().ContainSingle().Subject;
        link.Status.Should().Be(DoctorServiceStatus.Approved);
        link.DecidedAt.Should().Be(Now);
    }

    [Fact]
    public void Bekleyen_talep_dogrudan_atamayla_onaylanir()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();
        doctor.RequestService(serviceId, doctor.Id, Now);

        doctor.AssignService(serviceId, AdminId, Now);

        doctor.FindService(serviceId)!.Status.Should().Be(DoctorServiceStatus.Approved);
    }

    [Fact]
    public void RemoveService_kaydi_siler_olmayan_icin_false_doner()
    {
        var doctor = NewDoctor();
        var serviceId = Guid.NewGuid();
        doctor.RequestService(serviceId, doctor.Id, Now);

        doctor.RemoveService(serviceId).Should().BeTrue();
        doctor.RemoveService(serviceId).Should().BeFalse();
        doctor.Services.Should().BeEmpty();
    }

    // ---------- Uygunluk ----------

    [Fact]
    public void Uygunluk_sube_saatleri_icinde_eklenir()
    {
        var doctor = NewDoctor();

        var availability = doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null, HospitalHours());

        availability.Day.Should().Be(DayOfWeek.Monday);
        availability.ValidTo.Should().BeNull();
        doctor.Availabilities.Should().ContainSingle();
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 7, 0, 12, 0)]    // açılıştan önce başlar
    [InlineData(DayOfWeek.Monday, 16, 0, 18, 0)]   // kapanıştan sonra biter
    [InlineData(DayOfWeek.Sunday, 9, 0, 12, 0)]    // şube kapalı gün
    [InlineData(DayOfWeek.Saturday, 12, 0, 14, 0)] // cumartesi kapanıştan sonra
    public void Sube_saatleri_disindaki_uygunluk_reddedilir(DayOfWeek day, int sh, int sm, int eh, int em)
    {
        var doctor = NewDoctor();

        var act = () => doctor.AddAvailability(day, new TimeOnly(sh, sm), new TimeOnly(eh, em), Today, null, HospitalHours());

        act.Should().Throw<DomainException>();
        doctor.Availabilities.Should().BeEmpty();
    }

    [Fact]
    public void Bitis_baslangictan_once_veya_esit_olamaz()
    {
        var doctor = NewDoctor();

        ((Action)(() => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(12, 0), Today, null, HospitalHours()))).Should().Throw<DomainException>();
        ((Action)(() => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(9, 0), Today, null, HospitalHours()))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Gecerlilik_bitisi_baslangictan_once_olamaz()
    {
        var doctor = NewDoctor();

        var act = () => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, Today.AddDays(-1), HospitalHours());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Ayni_gun_kesisen_saat_ve_donem_cakisir_bitisik_cakismaz()
    {
        var doctor = NewDoctor();
        var hours = HospitalHours();
        doctor.AddAvailability(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null, hours);

        var overlapping = () => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(14, 0), Today, null, hours);
        var adjacent = () => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(15, 0), Today, null, hours);

        overlapping.Should().Throw<DomainException>();
        adjacent.Should().NotThrow();
        doctor.Availabilities.Should().HaveCount(2);
    }

    [Fact]
    public void Ayni_saat_farkli_gun_veya_ayrik_donem_cakismaz()
    {
        var doctor = NewDoctor();
        var hours = HospitalHours();
        doctor.AddAvailability(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, Today.AddDays(30), hours);

        var otherDay = () => doctor.AddAvailability(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null, hours);
        var laterPeriod = () => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today.AddDays(31), null, hours);
        var overlappingPeriod = () => doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today.AddDays(30), null, hours);

        otherDay.Should().NotThrow();
        laterPeriod.Should().NotThrow();
        overlappingPeriod.Should().Throw<DomainException>();
    }

    [Fact]
    public void Uygunluk_guncellenirken_kendisiyle_cakisma_sayilmaz()
    {
        var doctor = NewDoctor();
        var hours = HospitalHours();
        var availability = doctor.AddAvailability(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null, hours);

        doctor.UpdateAvailability(availability.Id, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(13, 0), Today, null, hours);

        availability.StartTime.Should().Be(new TimeOnly(10, 0));
        availability.EndTime.Should().Be(new TimeOnly(13, 0));
    }

    [Fact]
    public void Baska_uygunlukla_cakisan_guncelleme_reddedilir()
    {
        var doctor = NewDoctor();
        var hours = HospitalHours();
        doctor.AddAvailability(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null, hours);
        var second = doctor.AddAvailability(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(15, 0), Today, null, hours);

        var act = () => doctor.UpdateAvailability(
            second.Id, DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(15, 0), Today, null, hours);

        act.Should().Throw<DomainException>();
        second.StartTime.Should().Be(new TimeOnly(13, 0));
    }

    [Fact]
    public void RemoveAvailability_kaydi_siler()
    {
        var doctor = NewDoctor();
        var availability = doctor.AddAvailability(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null, HospitalHours());

        doctor.RemoveAvailability(availability.Id).Should().BeTrue();
        doctor.RemoveAvailability(availability.Id).Should().BeFalse();
    }

    // ---------- İzin ----------

    [Fact]
    public void Izin_eklenir_ve_UTC_saklanir()
    {
        var doctor = NewDoctor();
        var start = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(3));

        var timeOff = doctor.AddTimeOff(start, start.AddDays(2), " Yıllık izin ", Now);

        timeOff.StartAt.Offset.Should().Be(TimeSpan.Zero);
        timeOff.StartAt.Should().Be(start);
        timeOff.Reason.Should().Be("Yıllık izin");
    }

    [Fact]
    public void Gecmiste_biten_izin_eklenemez()
    {
        var doctor = NewDoctor();

        var act = () => doctor.AddTimeOff(Now.AddDays(-3), Now.AddDays(-1), null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Izin_bitisi_baslangictan_sonra_olmalidir()
    {
        var doctor = NewDoctor();

        var act = () => doctor.AddTimeOff(Now.AddDays(2), Now.AddDays(1), null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Kesisen_izin_reddedilir_bitisik_izin_kabul_edilir()
    {
        var doctor = NewDoctor();
        doctor.AddTimeOff(Now.AddDays(1), Now.AddDays(3), null, Now);

        var overlapping = () => doctor.AddTimeOff(Now.AddDays(2), Now.AddDays(4), null, Now);
        var adjacent = () => doctor.AddTimeOff(Now.AddDays(3), Now.AddDays(4), null, Now);

        overlapping.Should().Throw<DomainException>();
        adjacent.Should().NotThrow();
    }

    [Fact]
    public void RemoveTimeOff_kaydi_siler()
    {
        var doctor = NewDoctor();
        var timeOff = doctor.AddTimeOff(Now.AddDays(1), Now.AddDays(2), null, Now);

        doctor.RemoveTimeOff(timeOff.Id).Should().BeTrue();
        doctor.TimeOffs.Should().BeEmpty();
    }
}
