using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Users;

namespace Hospital.UnitTests.Domain;

public class PatientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static Patient NewPatient(string email = "  Hasta@Example.Test ") =>
        Patient.Register(email, "hash", " Ayşe ", " Yılmaz ", "10000000146", new DateOnly(1990, 5, 17), null, Now);

    [Fact]
    public void Register_alanlari_normallestirir_ve_hasta_rolu_atar()
    {
        var patient = NewPatient();

        patient.Email.Should().Be("hasta@example.test");
        patient.FullName.Should().Be("Ayşe Yılmaz");
        patient.Role.Should().Be(UserRole.Patient);
        patient.IsActive.Should().BeTrue();
        patient.Id.Should().NotBe(Guid.Empty);
        patient.ScopeClaims.Should().BeEmpty();
    }

    [Fact]
    public void Hasta_User_olarak_kullanilabilir_cok_bicimlilik()
    {
        User user = NewPatient();

        user.FullName.Should().Be("Ayşe Yılmaz");
        user.Should().BeOfType<Patient>();
    }

    [Fact]
    public void Register_gecersiz_TC_ile_DomainException_firlatir()
    {
        var act = () => Patient.Register("a@b.test", "hash", "Ayşe", "Yılmaz", "12345678901", new DateOnly(1990, 1, 1), null, Now);

        act.Should().Throw<DomainException>().WithMessage("*kimlik*");
    }

    [Fact]
    public void Register_gelecek_dogum_tarihi_ile_DomainException_firlatir()
    {
        var act = () => Patient.Register("a@b.test", "hash", "Ayşe", "Yılmaz", "10000000146", new DateOnly(2030, 1, 1), null, Now);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("", "Yılmaz")]
    [InlineData("Ayşe", " ")]
    public void Register_bos_ad_soyad_ile_DomainException_firlatir(string first, string last)
    {
        var act = () => Patient.Register("a@b.test", "hash", first, last, "10000000146", new DateOnly(1990, 1, 1), null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Ardisik_basarisiz_giris_hesabi_kilitler_ve_sayaci_sifirlar()
    {
        var patient = NewPatient();
        var lockout = TimeSpan.FromMinutes(15);

        for (var i = 0; i < 4; i++)
        {
            patient.RecordFailedLogin(Now, 5, lockout);
        }

        patient.IsLockedOut(Now).Should().BeFalse();

        patient.RecordFailedLogin(Now, 5, lockout);

        patient.IsLockedOut(Now).Should().BeTrue();
        patient.IsLockedOut(Now.AddMinutes(14)).Should().BeTrue();
        patient.IsLockedOut(Now.AddMinutes(16)).Should().BeFalse();
        patient.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public void Basarili_giris_sayaci_ve_kilidi_temizler()
    {
        var patient = NewPatient();
        patient.RecordFailedLogin(Now, 2, TimeSpan.FromMinutes(15));
        patient.RecordFailedLogin(Now, 2, TimeSpan.FromMinutes(15));

        patient.RecordSuccessfulLogin(Now.AddMinutes(20));

        patient.FailedLoginCount.Should().Be(0);
        patient.LockoutEnd.Should().BeNull();
        patient.LastLoginAt.Should().Be(Now.AddMinutes(20));
    }

    [Fact]
    public void Pasife_alma_ve_parola_degisimi_security_stamp_i_yeniler()
    {
        var patient = NewPatient();
        var initial = patient.SecurityStamp;

        patient.Deactivate();
        var afterDeactivate = patient.SecurityStamp;
        patient.ChangePasswordHash("yeni-hash");

        patient.IsActive.Should().BeFalse();
        afterDeactivate.Should().NotBe(initial);
        patient.SecurityStamp.Should().NotBe(afterDeactivate);
        patient.PasswordHash.Should().Be("yeni-hash");
    }
}
