using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

public class HospitalManagementTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Seed_varsayilan_kurum_ve_subeyi_olusturur_tek_subeli_kullanim_hazirdir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);

        var response = await superAdmin.GetAsync("/api/hospitals");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var hospitals = await ApiClient.ReadJsonAsync(response);
        hospitals.EnumerateArray().Should().Contain(h => h.GetProperty("name").GetString() == "Merkez Şube");

        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{hospitalId}"));
        detail.GetProperty("timeZoneId").GetString().Should().Be("Europe/Istanbul");

        // Hafta içi 5 gün + Cumartesi; Pazar kapalı.
        var days = detail.GetProperty("workingHours").EnumerateArray().Select(w => w.GetProperty("day").GetInt32()).ToList();
        days.Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);
        days.Should().NotContain(0);
    }

    [Fact]
    public async Task Super_yonetici_sube_ekler_ve_listede_gorur()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var name = $"Yeni Şube {Guid.NewGuid():N}";

        var id = await AdminSessions.CreateHospitalAsync(superAdmin, name);

        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{id}"));
        detail.GetProperty("name").GetString().Should().Be(name);
        detail.GetProperty("isActive").GetBoolean().Should().BeTrue();
        detail.GetProperty("workingHours").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Ayni_isimde_ikinci_sube_buyuk_kucuk_harf_fark_etmeksizin_409_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var name = $"Tekrar Şube {Guid.NewGuid():N}";
        await AdminSessions.CreateHospitalAsync(superAdmin, name);

        var response = await superAdmin.PostAsync("/api/hospitals", new
        {
            Name = name.ToUpperInvariant(),
            TimeZoneId = "Europe/Istanbul",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Gecersiz_saat_dilimi_alan_bazli_400_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);

        var response = await superAdmin.PostAsync("/api/hospitals", new { Name = "Hatalı", TimeZoneId = "Mars/Olympus" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ApiClient.ReadJsonAsync(response);
        body.GetProperty("errors").TryGetProperty("timeZoneId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Sube_yoneticisi_yeni_sube_ekleyemez_403()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, hospitalId);

        var response = await branchManager.PostAsync("/api/hospitals", new { Name = "İzinsiz", TimeZoneId = "Europe/Istanbul" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sube_yoneticisi_yalnizca_kendi_subesini_listeler()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        var hospitals = await ApiClient.ReadJsonAsync(await branchManager.GetAsync("/api/hospitals"));

        var ids = hospitals.EnumerateArray().Select(h => h.GetProperty("id").GetGuid()).ToList();
        ids.Should().ContainSingle().Which.Should().Be(own);
        ids.Should().NotContain(other);
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subeyi_goremez_guncelleyemez_pasiflestiremez_404()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        (await branchManager.GetAsync($"/api/hospitals/{other}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await branchManager.PutAsync($"/api/hospitals/{other}", new { Name = "Ele geçirildi", TimeZoneId = "Europe/Istanbul" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await branchManager.PutAsync($"/api/hospitals/{other}/working-hours", new { Hours = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await branchManager.PostAsync($"/api/hospitals/{other}/deactivate"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Diğer şube dokunulmamış kalmalı.
        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{other}"));
        detail.GetProperty("name").GetString().Should().NotBe("Ele geçirildi");
        detail.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Sube_yoneticisi_kendi_subesini_guncelleyebilir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);
        var newName = $"Güncel Şube {Guid.NewGuid():N}";

        var response = await branchManager.PutAsync(
            $"/api/hospitals/{own}",
            new { Name = newName, Address = "Yeni Adres 5", Phone = "02161112233", TimeZoneId = "Europe/Istanbul" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await ApiClient.ReadJsonAsync(await branchManager.GetAsync($"/api/hospitals/{own}"));
        detail.GetProperty("name").GetString().Should().Be(newName);
        detail.GetProperty("address").GetString().Should().Be("Yeni Adres 5");
    }

    [Fact]
    public async Task Baska_subenin_ismine_guncelleme_409_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var takenName = $"Dolu İsim {Guid.NewGuid():N}";
        await AdminSessions.CreateHospitalAsync(superAdmin, takenName);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);

        var response = await superAdmin.PutAsync($"/api/hospitals/{other}", new { Name = takenName, TimeZoneId = "Europe/Istanbul" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Calisma_saatleri_ayarlanir_pazartesi_basli_siralanir_ve_listede_olmayan_gun_kapanir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var id = await AdminSessions.CreateHospitalAsync(superAdmin);

        var first = await superAdmin.PutAsync($"/api/hospitals/{id}/working-hours", new
        {
            Hours = new[]
            {
                new { Day = 0, OpenTime = "10:00", CloseTime = "14:00" },  // Pazar
                new { Day = 1, OpenTime = "08:00", CloseTime = "17:00" },  // Pazartesi
                new { Day = 3, OpenTime = "09:30", CloseTime = "18:00" },  // Çarşamba
            },
        });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{id}"));
        detail.GetProperty("workingHours").EnumerateArray().Select(w => w.GetProperty("day").GetInt32())
            .Should().Equal(1, 3, 0); // Pazartesi'den başlayan sıra, Pazar en sonda

        // Yeniden ayarlama: Pazartesi güncellenir, Çarşamba/Pazar listede olmadığı için kapanır, Cuma eklenir.
        var second = await superAdmin.PutAsync($"/api/hospitals/{id}/working-hours", new
        {
            Hours = new[]
            {
                new { Day = 1, OpenTime = "07:00", CloseTime = "12:00" },
                new { Day = 5, OpenTime = "13:00", CloseTime = "16:00" },
            },
        });
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{id}"));
        var hours = detail.GetProperty("workingHours").EnumerateArray().ToList();
        hours.Select(w => w.GetProperty("day").GetInt32()).Should().Equal(1, 5);
        hours[0].GetProperty("openTime").GetString().Should().Be("07:00:00");
        hours[0].GetProperty("closeTime").GetString().Should().Be("12:00:00");
    }

    [Fact]
    public async Task Bos_liste_gonderilirse_sube_tum_gunler_kapali_olur()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var id = await AdminSessions.CreateHospitalAsync(superAdmin);
        await superAdmin.PutAsync($"/api/hospitals/{id}/working-hours", new
        {
            Hours = new[] { new { Day = 2, OpenTime = "08:00", CloseTime = "09:00" } },
        });

        var response = await superAdmin.PutAsync($"/api/hospitals/{id}/working-hours", new { Hours = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{id}"));
        detail.GetProperty("workingHours").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("17:00", "08:00")] // kapanış açılıştan önce
    [InlineData("08:00", "08:00")] // sıfır uzunluk
    public async Task Kapanis_acilistan_sonra_degilse_400_doner(string open, string close)
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var id = await AdminSessions.CreateHospitalAsync(superAdmin);

        var response = await superAdmin.PutAsync($"/api/hospitals/{id}/working-hours", new
        {
            Hours = new[] { new { Day = 1, OpenTime = open, CloseTime = close } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ayni_gune_iki_aralik_verilirse_400_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var id = await AdminSessions.CreateHospitalAsync(superAdmin);

        var response = await superAdmin.PutAsync($"/api/hospitals/{id}/working-hours", new
        {
            Hours = new[]
            {
                new { Day = 1, OpenTime = "08:00", CloseTime = "12:00" },
                new { Day = 1, OpenTime = "13:00", CloseTime = "17:00" },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sube_pasiflestirilir_silinmez_ve_katalogdan_kaybolur()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var id = await AdminSessions.CreateHospitalAsync(superAdmin);
        var anonymous = fixture.CreateClient();

        var before = await ApiClient.ReadJsonAsync(await anonymous.GetAsync("/api/catalog/hospitals"));
        before.EnumerateArray().Should().Contain(h => h.GetProperty("id").GetGuid() == id);

        var response = await superAdmin.PostAsync($"/api/hospitals/{id}/deactivate");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{id}"));
        detail.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var after = await ApiClient.ReadJsonAsync(await anonymous.GetAsync("/api/catalog/hospitals"));
        after.EnumerateArray().Should().NotContain(h => h.GetProperty("id").GetGuid() == id);
    }

    [Theory]
    [InlineData("GET", "/api/hospitals")]
    [InlineData("GET", "/api/services")]
    [InlineData("GET", "/api/organization")]
    public async Task Giris_yapmayan_yonetim_uclarinda_401_alir(string method, string url)
    {
        var anonymous = fixture.CreateClient();

        var response = await anonymous.SendAsync(new HttpMethod(method), url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/hospitals")]
    [InlineData("GET", "/api/services")]
    [InlineData("GET", "/api/organization")]
    [InlineData("POST", "/api/hospitals")]
    [InlineData("POST", "/api/services")]
    public async Task Hasta_yonetim_uclarina_giremez_403(string method, string url)
    {
        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        var body = method == "POST" ? new { Name = "x" } : null;
        var response = await patient.SendAsync(new HttpMethod(method), url, body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
