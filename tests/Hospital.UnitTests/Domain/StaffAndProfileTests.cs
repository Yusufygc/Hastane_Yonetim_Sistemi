using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Domain;

public class StaffAndProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Staff_Create_alanlari_normallestirir_ve_personel_rolu_atar()
    {
        var hospitalId = Guid.NewGuid();

        var staff = Staff.Create(" Personel@Example.Test ", "hash", " Mehmet ", " Kaya ", " Hemşire ", hospitalId, null, Now);

        staff.Email.Should().Be("personel@example.test");
        staff.FullName.Should().Be("Mehmet Kaya");
        staff.Title.Should().Be("Hemşire");
        staff.Role.Should().Be(UserRole.Staff);
        staff.HospitalId.Should().Be(hospitalId);
        staff.DepartmentId.Should().BeNull();
    }

    [Fact]
    public void Staff_Create_sube_olmadan_DomainException_firlatir()
    {
        var act = () => Staff.Create("s@e.test", "hash", "Mehmet", "Kaya", "Hemşire", Guid.Empty, null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TC_kimlik_no_maskelenir_ilk_uc_ve_son_iki_hane_gorunur()
    {
        TurkishIdentityNumber.Mask("10000000146").Should().Be("100******46");
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void Gecersiz_uzunluktaki_deger_tamamen_maskelenir(string value)
    {
        TurkishIdentityNumber.Mask(value).Should().Be(new string('*', value.Length));
    }

    [Fact]
    public void Hasta_profilini_gunceller_TC_ve_dogum_tarihi_degismez()
    {
        var patient = Patient.Register("h@e.test", "hash", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 5, 17), null, Now);

        patient.UpdateProfile(" Fatma ", " Demir ", " 0555 ", " ab+ ");

        patient.FirstName.Should().Be("Fatma");
        patient.LastName.Should().Be("Demir");
        patient.Phone.Should().Be("0555");
        patient.BloodType.Should().Be("AB+");
        patient.IdentityNumber.Should().Be("10000000146");
        patient.BirthDate.Should().Be(new DateOnly(1990, 5, 17));
    }

    [Fact]
    public void Kan_grubu_bos_birakilirsa_temizlenir()
    {
        var patient = Patient.Register("h@e.test", "hash", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 5, 17), null, Now);
        patient.UpdateProfile("Ayşe", "Yılmaz", null, "0+");

        patient.UpdateProfile("Ayşe", "Yılmaz", null, "  ");

        patient.BloodType.Should().BeNull();
    }

    [Fact]
    public void Gecersiz_kan_grubu_reddedilir()
    {
        var patient = Patient.Register("h@e.test", "hash", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 5, 17), null, Now);

        var act = () => patient.UpdateProfile("Ayşe", "Yılmaz", null, "C+");

        act.Should().Throw<DomainException>();
        patient.BloodType.Should().BeNull();
    }
}
