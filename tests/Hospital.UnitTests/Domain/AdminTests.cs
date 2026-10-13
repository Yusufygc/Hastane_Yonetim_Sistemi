using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Domain;

public class AdminTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuperAdmin_organizasyon_kapsami_tasir_sube_claimi_yoktur()
    {
        var organizationId = Guid.NewGuid();

        var admin = Admin.CreateSuperAdmin(" Admin@Test.Local ", "hash", "Sistem", "Yöneticisi", organizationId, Now);

        admin.Role.Should().Be(UserRole.Admin);
        admin.AccessLevel.Should().Be(AdminAccessLevel.SuperAdmin);
        admin.HospitalId.Should().BeNull();
        admin.Email.Should().Be("admin@test.local");
        admin.FullName.Should().Be("Sistem Yöneticisi");
        admin.ScopeClaims.Should().Contain(ScopeClaimTypes.OrganizationId, organizationId.ToString());
        admin.ScopeClaims.Should().Contain(ScopeClaimTypes.AccessLevel, "SUPER_ADMIN");
        admin.ScopeClaims.Should().NotContainKey(ScopeClaimTypes.HospitalId);
    }

    [Fact]
    public void BranchManager_sube_kapsami_tasir()
    {
        var (organizationId, hospitalId) = (Guid.NewGuid(), Guid.NewGuid());

        var admin = Admin.CreateBranchManager("sube@test.local", "hash", "Şube", "Yöneticisi", organizationId, hospitalId, Now);

        admin.AccessLevel.Should().Be(AdminAccessLevel.BranchManager);
        admin.HospitalId.Should().Be(hospitalId);
        admin.ScopeClaims.Should().Contain(ScopeClaimTypes.HospitalId, hospitalId.ToString());
        admin.ScopeClaims.Should().Contain(ScopeClaimTypes.AccessLevel, "BRANCH_MANAGER");
    }

    [Fact]
    public void Admin_User_olarak_kullanilabilir_cok_bicimlilik()
    {
        User user = Admin.CreateSuperAdmin("a@b.test", "hash", "Ad", "Soyad", Guid.NewGuid(), Now);

        user.FullName.Should().Be("Ad Soyad");
        user.ScopeClaims.Should().NotBeEmpty();
    }

    [Fact]
    public void BranchManager_bos_sube_ile_DomainException_firlatir()
    {
        var act = () => Admin.CreateBranchManager("a@b.test", "hash", "Ad", "Soyad", Guid.NewGuid(), Guid.Empty, Now);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("SUPER_ADMIN", AdminAccessLevel.SuperAdmin)]
    [InlineData("BRANCH_MANAGER", AdminAccessLevel.BranchManager)]
    public void Erisim_seviyesi_adlari_gidis_donus_eslesir(string name, AdminAccessLevel level)
    {
        AdminAccessLevelNames.From(level).Should().Be(name);
        AdminAccessLevelNames.Parse(name).Should().Be(level);
    }
}
