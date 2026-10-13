using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Facility;

namespace Hospital.UnitTests.Domain;

public class HospitalBranchTests
{
    private static HospitalBranch NewBranch() =>
        HospitalBranch.Create(Guid.NewGuid(), " Merkez Şube ", " Adres 1 ", " ", "Europe/Istanbul");

    private static WorkingHoursInput Hours(DayOfWeek day, int openHour, int closeHour) =>
        new(day, new TimeOnly(openHour, 0), new TimeOnly(closeHour, 0));

    [Fact]
    public void Create_alanlari_temizler_ve_aktif_baslar()
    {
        var branch = NewBranch();

        branch.Name.Should().Be("Merkez Şube");
        branch.Address.Should().Be("Adres 1");
        branch.Phone.Should().BeNull();
        branch.IsActive.Should().BeTrue();
        branch.WorkingHours.Should().BeEmpty();
        branch.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    [InlineData("   ")]
    public void Gecersiz_saat_dilimi_DomainException_firlatir(string timeZone)
    {
        var act = () => HospitalBranch.Create(Guid.NewGuid(), "Şube", null, null, timeZone);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Bos_ad_veya_kurum_DomainException_firlatir()
    {
        ((Action)(() => HospitalBranch.Create(Guid.NewGuid(), " ", null, null, "Europe/Istanbul")))
            .Should().Throw<DomainException>();
        ((Action)(() => HospitalBranch.Create(Guid.Empty, "Şube", null, null, "Europe/Istanbul")))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Update_alanlari_degistirir()
    {
        var branch = NewBranch();

        branch.Update("Yeni Ad", null, "0212", "UTC");

        branch.Name.Should().Be("Yeni Ad");
        branch.Address.Should().BeNull();
        branch.Phone.Should().Be("0212");
        branch.TimeZoneId.Should().Be("UTC");
    }

    [Fact]
    public void SetWorkingHours_gunleri_ekler_gunceller_ve_listede_olmayani_kaldirir()
    {
        var branch = NewBranch();
        branch.SetWorkingHours([Hours(DayOfWeek.Monday, 8, 17), Hours(DayOfWeek.Tuesday, 8, 17), Hours(DayOfWeek.Sunday, 10, 12)]);
        var mondayId = branch.WorkingHours.Single(w => w.Day == DayOfWeek.Monday).Id;

        branch.SetWorkingHours([Hours(DayOfWeek.Monday, 9, 18), Hours(DayOfWeek.Friday, 8, 12)]);

        branch.WorkingHours.Select(w => w.Day).Should().BeEquivalentTo([DayOfWeek.Monday, DayOfWeek.Friday]);

        // Mevcut gün güncellenir (aynı satır), yeniden oluşturulmaz.
        var monday = branch.WorkingHours.Single(w => w.Day == DayOfWeek.Monday);
        monday.Id.Should().Be(mondayId);
        monday.OpenTime.Should().Be(new TimeOnly(9, 0));
        monday.CloseTime.Should().Be(new TimeOnly(18, 0));
    }

    [Fact]
    public void SetWorkingHours_bos_liste_tum_gunleri_kapatir()
    {
        var branch = NewBranch();
        branch.SetWorkingHours([Hours(DayOfWeek.Monday, 8, 17)]);

        branch.SetWorkingHours([]);

        branch.WorkingHours.Should().BeEmpty();
    }

    [Fact]
    public void SetWorkingHours_ayni_gune_iki_aralikta_DomainException_firlatir_ve_degisiklik_yapmaz()
    {
        var branch = NewBranch();
        branch.SetWorkingHours([Hours(DayOfWeek.Monday, 8, 17)]);

        var act = () => branch.SetWorkingHours([Hours(DayOfWeek.Tuesday, 8, 12), Hours(DayOfWeek.Tuesday, 13, 17)]);

        act.Should().Throw<DomainException>();
        branch.WorkingHours.Select(w => w.Day).Should().BeEquivalentTo([DayOfWeek.Monday]);
    }

    [Fact]
    public void SetWorkingHours_kapanis_acilistan_once_ise_DomainException_firlatir_ve_degisiklik_yapmaz()
    {
        var branch = NewBranch();
        branch.SetWorkingHours([Hours(DayOfWeek.Monday, 8, 17)]);

        var act = () => branch.SetWorkingHours([Hours(DayOfWeek.Monday, 9, 18), Hours(DayOfWeek.Friday, 17, 8)]);

        act.Should().Throw<DomainException>();

        // Atomik: ilk (geçerli) girdi de uygulanmamış olmalı.
        branch.WorkingHours.Single().Day.Should().Be(DayOfWeek.Monday);
        branch.WorkingHours.Single().OpenTime.Should().Be(new TimeOnly(8, 0));
    }

    [Fact]
    public void Deactivate_subeyi_pasiflestirir()
    {
        var branch = NewBranch();

        branch.Deactivate();

        branch.IsActive.Should().BeFalse();
    }
}
