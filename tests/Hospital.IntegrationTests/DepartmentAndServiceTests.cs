using System.Globalization;
using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

public class DepartmentAndServiceTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    // ---------- Bölümler ----------

    [Fact]
    public async Task Bolum_eklenir_ve_listelenir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);

        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId, "Nöroloji");

        var list = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{hospitalId}/departments"));
        list.EnumerateArray().Should().ContainSingle(d => d.GetProperty("id").GetGuid() == departmentId
                                                          && d.GetProperty("name").GetString() == "Nöroloji");
    }

    [Fact]
    public async Task Ayni_subede_ayni_isimli_bolum_409_baska_subede_serbest()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalA = await AdminSessions.CreateHospitalAsync(superAdmin);
        var hospitalB = await AdminSessions.CreateHospitalAsync(superAdmin);
        await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalA, "Ortopedi");

        var duplicate = await superAdmin.PostAsync($"/api/hospitals/{hospitalA}/departments", new { Name = "ortopedi" });
        var sameNameOtherHospital = await superAdmin.PostAsync($"/api/hospitals/{hospitalB}/departments", new { Name = "Ortopedi" });

        // "ortopedi" ile "Ortopedi" büyük/küçük harf farkı gözetilmeden aynı isimdir.
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        sameNameOtherHospital.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Isim_tekilligi_sunucu_kulturunden_bagimsizdir_Turkce_kulturde_I_ve_i_ayni_harf_sayilir()
    {
        // Regresyon: C# ToLower() tr-TR kültüründe "I" → "ı" yapar, PostgreSQL lower() ise "i"; karşılaştırma tutmayıp
        // yinelenen ad tekillik indeksine kadar gidiyor ve anlamsız bir çakışma mesajı veriyordu.
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
            var hospital = await AdminSessions.CreateHospitalAsync(superAdmin);
            await AdminSessions.CreateDepartmentAsync(superAdmin, hospital, "INCELEME");

            var duplicate = await superAdmin.PostAsync($"/api/hospitals/{hospital}/departments", new { Name = "Inceleme" });

            duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ApiClient.ReadJsonAsync(duplicate)).GetProperty("code").GetString().Should().Be("department.name_taken");
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subenin_bolumlerine_erisemez_404()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var otherDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, other);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        (await branchManager.GetAsync($"/api/hospitals/{other}/departments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PostAsync($"/api/hospitals/{other}/departments", new { Name = "Sızma" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PutAsync($"/api/departments/{otherDepartment}", new { Name = "Ele geçirildi" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PostAsync($"/api/departments/{otherDepartment}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Kendi şubesinde serbest.
        (await branchManager.PostAsync($"/api/hospitals/{own}/departments", new { Name = "Kendi Bölümüm" })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Bolum_yeniden_adlandirilir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId, "Eski Ad");

        var response = await superAdmin.PutAsync($"/api/departments/{departmentId}", new { Name = "Yeni Ad" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{hospitalId}/departments"));
        list.EnumerateArray().Single().GetProperty("name").GetString().Should().Be("Yeni Ad");
    }

    [Fact]
    public async Task Aktif_hizmeti_olan_bolum_pasiflestirilemez_hizmet_pasifse_pasiflestirilir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var serviceId = await AdminSessions.CreateServiceAsync(superAdmin, hospitalId, departmentId);

        var blocked = await superAdmin.PostAsync($"/api/departments/{departmentId}/deactivate");
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await superAdmin.PostAsync($"/api/services/{serviceId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var allowed = await superAdmin.PostAsync($"/api/departments/{departmentId}/deactivate");

        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/hospitals/{hospitalId}/departments"));
        list.EnumerateArray().Single().GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    // ---------- Hizmetler ----------

    [Fact]
    public async Task Hizmet_sure_ve_ucretiyle_eklenir_ve_okunur()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);

        var serviceId = await AdminSessions.CreateServiceAsync(superAdmin, hospitalId, departmentId, "MR Çekimi", 45, 1250.50m);

        var service = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/services/{serviceId}"));
        service.GetProperty("name").GetString().Should().Be("MR Çekimi");
        service.GetProperty("durationMinutes").GetInt32().Should().Be(45);
        service.GetProperty("price").GetDecimal().Should().Be(1250.50m);
        service.GetProperty("currency").GetString().Should().Be("TRY");
        service.GetProperty("departmentId").GetGuid().Should().Be(departmentId);
        service.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Hizmet_bolumsuz_da_eklenebilir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);

        var serviceId = await AdminSessions.CreateServiceAsync(superAdmin, hospitalId, departmentId: null);

        var service = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/services/{serviceId}"));
        service.GetProperty("departmentId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Theory]
    [InlineData(0, 100, "TRY", "durationMinutes")]      // süre sıfır
    [InlineData(481, 100, "TRY", "durationMinutes")]    // süre üst sınırın üstünde
    [InlineData(20, -1, "TRY", "price")]                // negatif ücret
    [InlineData(20, 10.123, "TRY", "price")]            // 2'den fazla ondalık
    [InlineData(20, 100, "TR", "currency")]             // para birimi 3 harf değil
    [InlineData(20, 100, "T1Y", "currency")]            // harf değil
    public async Task Gecersiz_hizmet_alan_bazli_400_doner(int duration, decimal price, string currency, string expectedField)
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);

        var response = await superAdmin.PostAsync("/api/services", new
        {
            HospitalId = hospitalId,
            Name = "Geçersiz",
            DurationMinutes = duration,
            Price = price,
            Currency = currency,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ApiClient.ReadJsonAsync(response);
        body.GetProperty("errors").TryGetProperty(expectedField, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Baska_subenin_bolumune_hizmet_baglanamaz_400()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalA = await AdminSessions.CreateHospitalAsync(superAdmin);
        var hospitalB = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentOfB = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalB);

        var response = await superAdmin.PostAsync("/api/services", new
        {
            HospitalId = hospitalA,
            DepartmentId = departmentOfB,
            Name = "Yanlış Bölüm",
            DurationMinutes = 20,
            Price = 100m,
            Currency = "TRY",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("service.department_invalid");
    }

    [Fact]
    public async Task Pasif_bolume_hizmet_baglanamaz_400()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        await superAdmin.PostAsync($"/api/departments/{departmentId}/deactivate");

        var response = await superAdmin.PostAsync("/api/services", new
        {
            HospitalId = hospitalId,
            DepartmentId = departmentId,
            Name = "Pasif Bölümde",
            DurationMinutes = 20,
            Price = 100m,
            Currency = "TRY",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ayni_subede_ayni_isimli_hizmet_409()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        await AdminSessions.CreateServiceAsync(superAdmin, hospitalId, name: "Ultrason");

        var response = await superAdmin.PostAsync("/api/services", new
        {
            HospitalId = hospitalId,
            Name = "ultrason",
            DurationMinutes = 20,
            Price = 100m,
            Currency = "TRY",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hizmet_guncellenir_fiyat_ve_sure_degisir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var serviceId = await AdminSessions.CreateServiceAsync(superAdmin, hospitalId, durationMinutes: 20, price: 300m);

        var response = await superAdmin.PutAsync($"/api/services/{serviceId}", new
        {
            DepartmentId = (Guid?)null,
            Name = "Yeni Hizmet Adı",
            DurationMinutes = 40,
            Price = 450.75m,
            Currency = "EUR",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var service = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/services/{serviceId}"));
        service.GetProperty("durationMinutes").GetInt32().Should().Be(40);
        service.GetProperty("price").GetDecimal().Should().Be(450.75m);
        service.GetProperty("currency").GetString().Should().Be("EUR");
    }

    [Fact]
    public async Task Hizmet_pasiflestirilir_silinmez_ve_katalogda_gorunmez()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var kept = await AdminSessions.CreateServiceAsync(superAdmin, hospitalId);
        var removed = await AdminSessions.CreateServiceAsync(superAdmin, hospitalId);
        var anonymous = fixture.CreateClient();

        (await superAdmin.PostAsync($"/api/services/{removed}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var admin = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/services/{removed}"));
        admin.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var catalog = await ApiClient.ReadJsonAsync(await anonymous.GetAsync($"/api/catalog/hospitals/{hospitalId}/services"));
        var ids = catalog.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(kept);
        ids.Should().NotContain(removed);
    }

    [Fact]
    public async Task Hizmet_listesi_sube_filtresiyle_ve_kapsamla_daralir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var ownService = await AdminSessions.CreateServiceAsync(superAdmin, own);
        var otherService = await AdminSessions.CreateServiceAsync(superAdmin, other);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        var filtered = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/services?hospitalId={own}"));
        filtered.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).Should().Equal(ownService);

        // Şube yöneticisi filtre vermese bile yalnız kendi şubesini görür.
        var scoped = await ApiClient.ReadJsonAsync(await branchManager.GetAsync("/api/services"));
        var scopedIds = scoped.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        scopedIds.Should().Contain(ownService);
        scopedIds.Should().NotContain(otherService);

        // Başka şubeyi filtre olarak vermek 404.
        (await branchManager.GetAsync($"/api/services?hospitalId={other}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subenin_hizmetini_goremez_guncelleyemez_pasiflestiremez_404()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var otherService = await AdminSessions.CreateServiceAsync(superAdmin, other, price: 500m);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        (await branchManager.GetAsync($"/api/services/{otherService}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await branchManager.PutAsync($"/api/services/{otherService}", new
        {
            Name = "Fiyat Oyunu",
            DurationMinutes = 10,
            Price = 1m,
            Currency = "TRY",
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await branchManager.PostAsync($"/api/services/{otherService}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await branchManager.PostAsync("/api/services", new
        {
            HospitalId = other,
            Name = "Sızma",
            DurationMinutes = 10,
            Price = 1m,
            Currency = "TRY",
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Diğer şubenin hizmeti değişmemiş olmalı.
        var service = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/services/{otherService}"));
        service.GetProperty("price").GetDecimal().Should().Be(500m);
        service.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }
}
