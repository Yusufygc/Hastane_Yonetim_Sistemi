using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

public class CatalogAndOrganizationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    // ---------- Anonim katalog ----------

    [Fact]
    public async Task Katalog_giris_yapmadan_sube_calisma_saati_bolum_ve_hizmetleri_dondurur()
    {
        var anonymous = fixture.CreateClient();

        var hospitalsResponse = await anonymous.GetAsync("/api/catalog/hospitals");
        hospitalsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var hospitals = await ApiClient.ReadJsonAsync(hospitalsResponse);
        var seeded = hospitals.EnumerateArray().Single(h => h.GetProperty("name").GetString() == "Merkez Şube");
        seeded.GetProperty("workingHours").GetArrayLength().Should().Be(6);
        var hospitalId = seeded.GetProperty("id").GetGuid();

        var departments = await ApiClient.ReadJsonAsync(await anonymous.GetAsync($"/api/catalog/hospitals/{hospitalId}/departments"));
        departments.EnumerateArray().Select(d => d.GetProperty("name").GetString())
            .Should().Contain(["Dahiliye", "Kardiyoloji", "Radyoloji", "Laboratuvar"]);

        var services = await ApiClient.ReadJsonAsync(await anonymous.GetAsync($"/api/catalog/hospitals/{hospitalId}/services"));
        var genelMuayene = services.EnumerateArray().Single(s => s.GetProperty("name").GetString() == "Genel Muayene");
        genelMuayene.GetProperty("durationMinutes").GetInt32().Should().Be(15);
        genelMuayene.GetProperty("price").GetDecimal().Should().Be(500m);
        genelMuayene.GetProperty("currency").GetString().Should().Be("TRY");
    }

    [Fact]
    public async Task Katalog_yonetim_alanlarini_ve_kurum_kimligini_sizdirmaz()
    {
        var anonymous = fixture.CreateClient();

        var hospitals = await ApiClient.ReadJsonAsync(await anonymous.GetAsync("/api/catalog/hospitals"));

        foreach (var hospital in hospitals.EnumerateArray())
        {
            hospital.TryGetProperty("organizationId", out _).Should().BeFalse();
            hospital.TryGetProperty("isActive", out _).Should().BeFalse();
        }
    }

    [Fact]
    public async Task Katalog_pasif_bolum_ve_hizmetleri_gostermez()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var activeDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var inactiveDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        await superAdmin.PostAsync($"/api/departments/{inactiveDepartment}/deactivate");

        var anonymous = fixture.CreateClient();
        var departments = await ApiClient.ReadJsonAsync(await anonymous.GetAsync($"/api/catalog/hospitals/{hospitalId}/departments"));

        var ids = departments.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(activeDepartment);
        ids.Should().NotContain(inactiveDepartment);
    }

    [Fact]
    public async Task Pasif_veya_olmayan_subenin_katalogu_404_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var inactive = await AdminSessions.CreateHospitalAsync(superAdmin);
        await superAdmin.PostAsync($"/api/hospitals/{inactive}/deactivate");
        var anonymous = fixture.CreateClient();

        (await anonymous.GetAsync($"/api/catalog/hospitals/{inactive}/departments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/api/catalog/hospitals/{inactive}/services")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/api/catalog/hospitals/{Guid.NewGuid()}/services")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Organizasyon ----------

    [Fact]
    public async Task Super_yonetici_kurumunu_gorur_ve_yeniden_adlandirir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var newName = $"Kurum {Guid.NewGuid():N}";

        var update = await superAdmin.PutAsync("/api/organization", new { Name = newName });
        var get = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync("/api/organization"));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        get.GetProperty("name").GetString().Should().Be(newName);
    }

    [Fact]
    public async Task Bos_kurum_adi_400_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);

        var response = await superAdmin.PutAsync("/api/organization", new { Name = " " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sube_yoneticisi_kurum_ayarlarina_giremez_403()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, hospitalId);

        (await branchManager.GetAsync("/api/organization")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await branchManager.PutAsync("/api/organization", new { Name = "Ele geçirildi" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
