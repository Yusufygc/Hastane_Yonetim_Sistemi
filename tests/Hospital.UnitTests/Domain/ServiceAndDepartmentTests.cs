using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Facility;

namespace Hospital.UnitTests.Domain;

public class ServiceAndDepartmentTests
{
    private static Service NewService(int duration = 15, decimal price = 500m, string currency = "try") =>
        Service.Create(Guid.NewGuid(), null, " Genel Muayene ", duration, price, currency);

    [Fact]
    public void Service_Create_alanlari_normallestirir()
    {
        var service = NewService(price: 499.999m);

        service.Name.Should().Be("Genel Muayene");
        service.Currency.Should().Be("TRY");
        service.Price.Should().Be(500.00m);
        service.DurationMinutes.Should().Be(15);
        service.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(481)]
    public void Service_gecersiz_sure_DomainException_firlatir(int duration)
    {
        var act = () => NewService(duration: duration);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Service_negatif_ucret_DomainException_firlatir()
    {
        var act = () => NewService(price: -0.01m);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("TR")]
    [InlineData("TRYY")]
    [InlineData("T1Y")]
    public void Service_gecersiz_para_birimi_DomainException_firlatir(string currency)
    {
        var act = () => NewService(currency: currency);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Service_Update_ve_Deactivate_calisir()
    {
        var service = NewService();
        var departmentId = Guid.NewGuid();

        service.Update(departmentId, "MR", 45, 1250m, "eur");
        service.Deactivate();

        service.DepartmentId.Should().Be(departmentId);
        service.Name.Should().Be("MR");
        service.DurationMinutes.Should().Be(45);
        service.Price.Should().Be(1250m);
        service.Currency.Should().Be("EUR");
        service.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Service_Update_gecersiz_deger_ile_DomainException_firlatir()
    {
        var service = NewService();

        var act = () => service.Update(null, "MR", 0, 100m, "TRY");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Department_Create_Rename_Deactivate_calisir()
    {
        var hospitalId = Guid.NewGuid();
        var department = Department.Create(hospitalId, " Kardiyoloji ");

        department.Rename("Dahiliye");
        department.Deactivate();

        department.HospitalId.Should().Be(hospitalId);
        department.Name.Should().Be("Dahiliye");
        department.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Department_bos_ad_DomainException_firlatir()
    {
        var act = () => Department.Create(Guid.NewGuid(), " ");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Organization_Create_ve_Rename_calisir()
    {
        var organization = Organization.Create(" Sağlık Grubu ");

        organization.Rename("Yeni Grup");

        organization.Name.Should().Be("Yeni Grup");
        organization.IsActive.Should().BeTrue();
    }
}
