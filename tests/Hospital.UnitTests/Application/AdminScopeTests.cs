using AwesomeAssertions;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;

namespace Hospital.UnitTests.Application;

public class AdminScopeTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid OtherOrg = Guid.NewGuid();

    private sealed record FakeUser(
        string? Role,
        string? AccessLevel,
        Guid? OrganizationId,
        Guid? HospitalId) : ICurrentUser
    {
        public bool IsAuthenticated => Role is not null;

        public Guid? UserId => Guid.NewGuid();

        public string? Email => "x@test.local";
    }

    [Fact]
    public void SuperAdmin_kapsami_tum_organizasyon_subelerini_kapsar()
    {
        var scope = AdminScope.From(new FakeUser("ADMIN", "SUPER_ADMIN", Org, null));

        scope.Should().NotBeNull();
        scope!.IsSuperAdmin.Should().BeTrue();
        scope.CanAccessHospital(Org, Guid.NewGuid()).Should().BeTrue();
        scope.CanAccessHospital(Org, Guid.NewGuid()).Should().BeTrue();
        scope.CanAccessHospital(OtherOrg, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void BranchManager_yalnizca_kendi_subesine_erisir()
    {
        var hospital = Guid.NewGuid();
        var scope = AdminScope.From(new FakeUser("ADMIN", "BRANCH_MANAGER", Org, hospital));

        scope.Should().NotBeNull();
        scope!.IsSuperAdmin.Should().BeFalse();
        scope.CanAccessHospital(Org, hospital).Should().BeTrue();
        scope.CanAccessHospital(Org, Guid.NewGuid()).Should().BeFalse();
        scope.CanAccessHospital(OtherOrg, hospital).Should().BeFalse();
    }

    [Theory]
    [InlineData("PATIENT", "SUPER_ADMIN")]    // rol yönetici değil
    [InlineData("DOCTOR", null)]
    [InlineData(null, null)]                  // giriş yok
    [InlineData("ADMIN", null)]               // erişim seviyesi eksik
    [InlineData("ADMIN", "UNKNOWN")]          // bilinmeyen seviye
    public void Yonetici_olmayan_veya_eksik_claimli_kullanici_icin_kapsam_yoktur(string? role, string? accessLevel)
    {
        AdminScope.From(new FakeUser(role, accessLevel, Org, Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public void Organizasyon_claimi_yoksa_kapsam_yoktur()
    {
        AdminScope.From(new FakeUser("ADMIN", "SUPER_ADMIN", null, null)).Should().BeNull();
    }

    [Fact]
    public void BranchManager_sube_claimi_yoksa_kapsam_yoktur()
    {
        AdminScope.From(new FakeUser("ADMIN", "BRANCH_MANAGER", Org, null)).Should().BeNull();
    }
}
