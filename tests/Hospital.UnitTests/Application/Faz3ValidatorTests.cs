using AwesomeAssertions;
using Hospital.Application.Features.Admins.Commands.CreateAdmin;
using Hospital.Application.Features.Auth.Commands.ChangePassword;
using Hospital.Application.Features.Doctors.Commands.AddMyAvailability;
using Hospital.Application.Features.Doctors.Commands.AddMyTimeOff;
using Hospital.Application.Features.Doctors.Commands.AssignDoctorServices;
using Hospital.Application.Features.Doctors.Commands.CreateDoctor;
using Hospital.Application.Features.Doctors.Queries.ListDoctorServiceRequests;
using Hospital.Application.Features.Patients.Commands.UpdateMyPatientProfile;

namespace Hospital.UnitTests.Application;

public class Faz3ValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    // ---------- CreateDoctor ----------

    private static CreateDoctorCommand ValidDoctor() => new(
        Guid.NewGuid(), "doktor@example.test", "Test1234x", "Ayşe", "Yılmaz", "Dahiliye", "D-1", null);

    [Fact]
    public void Gecerli_doktor_komutu_dogrulamayi_gecer() =>
        new CreateDoctorValidator().Validate(ValidDoctor()).IsValid.Should().BeTrue();

    [Fact]
    public void Doktor_icin_bos_alanlar_ve_zayif_parola_alan_bazli_reddedilir()
    {
        var result = new CreateDoctorValidator().Validate(ValidDoctor() with
        {
            DepartmentId = Guid.Empty,
            Password = "kisa",
            Specialty = "",
            DiplomaNo = " ",
        });

        result.Errors.Select(e => e.PropertyName).Should().Contain(
            [nameof(CreateDoctorCommand.DepartmentId), nameof(CreateDoctorCommand.Password),
             nameof(CreateDoctorCommand.Specialty), nameof(CreateDoctorCommand.DiplomaNo)]);
    }

    // ---------- Uygunluk ----------

    private static AddMyAvailabilityCommand ValidAvailability() =>
        new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), Today, null);

    [Fact]
    public void Gecerli_uygunluk_dogrulamayi_gecer() =>
        new AddMyAvailabilityValidator().Validate(ValidAvailability()).IsValid.Should().BeTrue();

    [Fact]
    public void Bitis_baslangictan_once_olan_uygunluk_reddedilir()
    {
        var result = new AddMyAvailabilityValidator().Validate(
            ValidAvailability() with { StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(9, 0) });

        result.Errors.Should().Contain(e => e.PropertyName == nameof(AddMyAvailabilityCommand.EndTime));
    }

    [Fact]
    public void Gecerlilik_baslangici_yoksa_ve_bitis_oncesiyse_reddedilir()
    {
        var validator = new AddMyAvailabilityValidator();

        validator.Validate(ValidAvailability() with { ValidFrom = default }).Errors
            .Should().Contain(e => e.PropertyName == nameof(AddMyAvailabilityCommand.ValidFrom));
        validator.Validate(ValidAvailability() with { ValidTo = Today.AddDays(-1) }).Errors
            .Should().Contain(e => e.PropertyName == nameof(AddMyAvailabilityCommand.ValidTo));
    }

    [Fact]
    public void Gecersiz_gun_reddedilir() =>
        new AddMyAvailabilityValidator().Validate(ValidAvailability() with { Day = (DayOfWeek)9 }).Errors
            .Should().Contain(e => e.PropertyName == nameof(AddMyAvailabilityCommand.Day));

    // ---------- İzin ----------

    [Fact]
    public void Izin_bitisi_baslangictan_once_ise_reddedilir()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        var result = new AddMyTimeOffValidator().Validate(new AddMyTimeOffCommand(start, start.AddHours(-1), null));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(AddMyTimeOffCommand.EndAt));
    }

    [Fact]
    public void Cok_uzun_izin_nedeni_reddedilir()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        var result = new AddMyTimeOffValidator().Validate(new AddMyTimeOffCommand(start, start.AddDays(1), new string('x', 301)));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(AddMyTimeOffCommand.Reason));
    }

    // ---------- Hizmet atama / talep listesi ----------

    [Fact]
    public void Hizmet_atamasinda_liste_zorunlu_bos_liste_gecerlidir()
    {
        var validator = new AssignDoctorServicesValidator();

        validator.Validate(new AssignDoctorServicesCommand(Guid.NewGuid(), [])).IsValid.Should().BeTrue();
        validator.Validate(new AssignDoctorServicesCommand(Guid.NewGuid(), null!)).IsValid.Should().BeFalse();
        validator.Validate(new AssignDoctorServicesCommand(Guid.NewGuid(), [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("pending", true)]
    [InlineData("Approved", true)]
    [InlineData("REJECTED", true)]
    [InlineData("bekliyor", false)]
    public void Talep_listesi_durum_filtresi_dogrulanir(string? status, bool valid) =>
        new ListDoctorServiceRequestsValidator().Validate(new ListDoctorServiceRequestsQuery(status)).IsValid.Should().Be(valid);

    // ---------- Yönetici oluşturma ----------

    private static CreateAdminCommand ValidAdmin(string level = "BRANCH_MANAGER", Guid? hospitalId = null) =>
        new(level, level == "BRANCH_MANAGER" ? hospitalId ?? Guid.NewGuid() : hospitalId, "yonetici@example.test", "Test1234x", "Ali", "Veli");

    [Fact]
    public void Sube_yoneticisi_sube_ister_super_yonetici_sube_almaz()
    {
        var validator = new CreateAdminValidator();

        validator.Validate(ValidAdmin()).IsValid.Should().BeTrue();
        validator.Validate(ValidAdmin("SUPER_ADMIN")).IsValid.Should().BeTrue();
        validator.Validate(ValidAdmin() with { HospitalId = null }).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateAdminCommand.HospitalId));
        validator.Validate(ValidAdmin("SUPER_ADMIN", Guid.NewGuid())).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateAdminCommand.HospitalId));
    }

    [Fact]
    public void Bilinmeyen_erisim_seviyesi_reddedilir() =>
        new CreateAdminValidator().Validate(ValidAdmin() with { AccessLevel = "ROOT" }).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateAdminCommand.AccessLevel));

    // ---------- Parola değiştirme ----------

    [Fact]
    public void Parola_degistirmede_yeni_parola_kurali_uygulanir()
    {
        var validator = new ChangePasswordValidator();

        validator.Validate(new ChangePasswordCommand("Eski1234x", "Yeni1234x")).IsValid.Should().BeTrue();
        validator.Validate(new ChangePasswordCommand("", "Yeni1234x")).Errors
            .Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.CurrentPassword));
        validator.Validate(new ChangePasswordCommand("Eski1234x", "zayif")).Errors
            .Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    // ---------- Hasta profili ----------

    [Theory]
    [InlineData(null, true)]
    [InlineData("A+", true)]
    [InlineData(" 0- ", true)]
    [InlineData("C+", false)]
    public void Hasta_profilinde_kan_grubu_dogrulanir(string? bloodType, bool valid) =>
        new UpdateMyPatientProfileValidator()
            .Validate(new UpdateMyPatientProfileCommand("Ayşe", "Yılmaz", null, bloodType)).IsValid.Should().Be(valid);
}
