using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>Doktor-hizmet talep/onay akışı ve bunun katalogdaki yansıması.</summary>
public class DoctorServiceFlowTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Setup(ApiClient Admin, Guid HospitalId, Guid DepartmentId, Guid DoctorId, ApiClient Doctor);

    private async Task<Setup> NewSetupAsync()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(admin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(admin, hospitalId);
        var (doctorId, email) = await AdminSessions.CreateDoctorAsync(admin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);
        return new Setup(admin, hospitalId, departmentId, doctorId, doctor);
    }

    private static async Task<string> StatusOfAsync(ApiClient doctor, Guid serviceId)
    {
        var services = await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me/services"));
        return services.EnumerateArray()
            .Single(s => s.GetProperty("serviceId").GetGuid() == serviceId)
            .GetProperty("status").GetString()!;
    }

    private async Task<JsonElement> CatalogDoctorsAsync(Guid hospitalId, Guid? serviceId = null)
    {
        var url = $"/api/catalog/doctors?hospitalId={hospitalId}" + (serviceId is null ? "" : $"&serviceId={serviceId}");
        var response = await fixture.CreateClient().GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ApiClient.ReadJsonAsync(response);
    }

    [Fact]
    public async Task Talep_bekler_onaylaninca_onayli_olur_ve_katalogda_gorunur()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);

        var request = await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId });
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await StatusOfAsync(s.Doctor, serviceId)).Should().Be("PENDING");

        // Onaylanana kadar katalogda yok (randevu alınamaz).
        (await CatalogDoctorsAsync(s.HospitalId, serviceId)).EnumerateArray()
            .Should().NotContain(d => d.GetProperty("id").GetGuid() == s.DoctorId);

        var pending = await ApiClient.ReadJsonAsync(await s.Admin.GetAsync("/api/doctor-service-requests"));
        pending.EnumerateArray().Should().Contain(r => r.GetProperty("doctorId").GetGuid() == s.DoctorId
                                                       && r.GetProperty("serviceId").GetGuid() == serviceId
                                                       && r.GetProperty("status").GetString() == "PENDING");

        var approve = await s.Admin.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(s.Doctor, serviceId)).Should().Be("APPROVED");

        var catalog = await CatalogDoctorsAsync(s.HospitalId, serviceId);
        var listed = catalog.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == s.DoctorId);
        listed.GetProperty("services").EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == serviceId);

        // Onaylanan talep artık bekleyenler listesinde değil.
        var pendingAfter = await ApiClient.ReadJsonAsync(await s.Admin.GetAsync("/api/doctor-service-requests?status=pending"));
        pendingAfter.EnumerateArray().Should().NotContain(r => r.GetProperty("doctorId").GetGuid() == s.DoctorId);
        var approved = await ApiClient.ReadJsonAsync(await s.Admin.GetAsync("/api/doctor-service-requests?status=APPROVED"));
        approved.EnumerateArray().Should().Contain(r => r.GetProperty("doctorId").GetGuid() == s.DoctorId);
    }

    [Fact]
    public async Task Katalog_yanitinda_kisisel_alan_yoktur()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceId } });

        var catalog = await CatalogDoctorsAsync(s.HospitalId, serviceId);
        var doctor = catalog.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == s.DoctorId);

        doctor.TryGetProperty("email", out _).Should().BeFalse();
        doctor.TryGetProperty("phone", out _).Should().BeFalse();
        doctor.TryGetProperty("diplomaNo", out _).Should().BeFalse();
        doctor.GetProperty("firstName").GetString().Should().Be("Mehmet");
    }

    [Fact]
    public async Task Hizmeti_olmayan_doktor_katalogda_yoktur_pasif_doktor_da_yoktur()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceId } });

        (await CatalogDoctorsAsync(s.HospitalId)).EnumerateArray()
            .Should().Contain(d => d.GetProperty("id").GetGuid() == s.DoctorId);

        (await s.Admin.PostAsync($"/api/users/{s.DoctorId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CatalogDoctorsAsync(s.HospitalId)).EnumerateArray()
            .Should().NotContain(d => d.GetProperty("id").GetGuid() == s.DoctorId);
    }

    [Fact]
    public async Task Katalog_serviceId_filtresi_yalniz_o_hizmeti_sunanlari_getirir()
    {
        var s = await NewSetupAsync();
        var serviceA = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        var serviceB = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceA } });

        (await CatalogDoctorsAsync(s.HospitalId, serviceA)).EnumerateArray()
            .Should().Contain(d => d.GetProperty("id").GetGuid() == s.DoctorId);
        (await CatalogDoctorsAsync(s.HospitalId, serviceB)).EnumerateArray()
            .Should().NotContain(d => d.GetProperty("id").GetGuid() == s.DoctorId);
    }

    [Fact]
    public async Task Katalog_doktorlari_anonim_erisilir_hospitalId_zorunlu_olmayan_sube_404()
    {
        var anonymous = fixture.CreateClient();

        var missing = await anonymous.GetAsync("/api/catalog/doctors");
        var unknown = await anonymous.GetAsync($"/api/catalog/doctors?hospitalId={Guid.NewGuid()}");

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiClient.ReadJsonAsync(unknown)).GetProperty("code").GetString().Should().Be("catalog.hospital_not_found");
    }

    [Fact]
    public async Task Seed_ornek_doktoru_katalogda_genel_muayene_ile_gorunur()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(admin);

        var catalog = await CatalogDoctorsAsync(hospitalId);

        catalog.EnumerateArray().Should().Contain(d =>
            d.GetProperty("services").EnumerateArray().Any(x => x.GetProperty("name").GetString() == "Genel Muayene"));

        var sample = await AdminSessions.LoginAsync(fixture, TestDoctor.SeededEmail, TestDoctor.Password);
        var availability = await ApiClient.ReadJsonAsync(await sample.GetAsync("/api/doctors/me/availability"));
        availability.GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task Baska_subenin_hizmeti_talep_edilemez_400()
    {
        var s = await NewSetupAsync();
        var otherHospital = await AdminSessions.CreateHospitalAsync(s.Admin);
        var foreignService = await AdminSessions.CreateServiceAsync(s.Admin, otherHospital);

        var response = await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = foreignService });
        var missing = await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("doctor.service_invalid");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pasif_hizmet_talep_edilemez()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        (await s.Admin.PostAsync($"/api/services/{serviceId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ayni_talep_tekrarlanamaz_409_onayli_hizmet_tekrar_talep_edilemez()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        (await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var again = await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(again)).GetProperty("code").GetString().Should().Be("doctor.service_already_requested");

        await s.Admin.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/approve");

        var afterApproval = await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId });
        afterApproval.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(afterApproval)).GetProperty("code").GetString().Should().Be("doctor.service_already_approved");
    }

    [Fact]
    public async Task Reddedilen_talep_yeniden_edilebilir_bekleyen_talep_geri_cekilir()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId });

        var reject = await s.Admin.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/reject");
        reject.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(s.Doctor, serviceId)).Should().Be("REJECTED");

        // Reddedilmiş talebi tekrar reddetmek mümkün değil (yalnız bekleyen reddedilir).
        var rejectAgain = await s.Admin.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/reject");
        rejectAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(rejectAgain)).GetProperty("code").GetString().Should().Be("doctor.service_not_pending");

        (await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await StatusOfAsync(s.Doctor, serviceId)).Should().Be("PENDING");

        (await s.Doctor.DeleteAsync($"/api/doctors/me/services/{serviceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var services = await ApiClient.ReadJsonAsync(await s.Doctor.GetAsync("/api/doctors/me/services"));
        services.EnumerateArray().Should().NotContain(x => x.GetProperty("serviceId").GetGuid() == serviceId);
    }

    [Fact]
    public async Task Doktor_onayli_hizmeti_kendisi_kaldiramaz_yonetici_kaldirir()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceId } });

        var byDoctor = await s.Doctor.DeleteAsync($"/api/doctors/me/services/{serviceId}");
        byDoctor.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(byDoctor)).GetProperty("code").GetString().Should().Be("doctor.service_approved_locked");

        (await s.Admin.DeleteAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await s.Admin.DeleteAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await s.Doctor.DeleteAsync($"/api/doctors/me/services/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yonetici_dogrudan_atama_onayli_kumeyi_listeyle_esitler()
    {
        var s = await NewSetupAsync();
        var serviceA = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        var serviceB = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        var serviceC = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceC });

        (await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceA, serviceB } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(s.Doctor, serviceA)).Should().Be("APPROVED");
        (await StatusOfAsync(s.Doctor, serviceB)).Should().Be("APPROVED");

        // Küme değişir: B kalkar, C (bekleyen talep) listeye girerek onaylanır; A idempotent.
        (await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceA, serviceC } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(s.Doctor, serviceA)).Should().Be("APPROVED");
        (await StatusOfAsync(s.Doctor, serviceC)).Should().Be("APPROVED");
        var services = await ApiClient.ReadJsonAsync(await s.Doctor.GetAsync("/api/doctors/me/services"));
        services.EnumerateArray().Should().NotContain(x => x.GetProperty("serviceId").GetGuid() == serviceB);

        // Boş liste tüm onaylı hizmetleri kaldırır.
        (await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = Array.Empty<Guid>() }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ApiClient.ReadJsonAsync(await s.Doctor.GetAsync("/api/doctors/me/services"))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Atamada_baska_subenin_veya_olmayan_hizmet_400_liste_zorunlu()
    {
        var s = await NewSetupAsync();
        var otherHospital = await AdminSessions.CreateHospitalAsync(s.Admin);
        var foreignService = await AdminSessions.CreateServiceAsync(s.Admin, otherHospital);

        var foreign = await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { foreignService } });
        var missing = await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { Guid.NewGuid() } });
        var noList = await s.Admin.PutAsync($"/api/doctors/{s.DoctorId}/services", new { });

        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noList.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Olmayan_talep_onaylanamaz_404_olmayan_doktor_404()
    {
        var s = await NewSetupAsync();
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);

        (await s.Admin.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/approve")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await s.Admin.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/reject")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await s.Admin.PostAsync($"/api/doctors/{Guid.NewGuid()}/services/{serviceId}/approve")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subenin_talebini_goremez_onaylayamaz()
    {
        var s = await NewSetupAsync();
        var ownHospital = await AdminSessions.CreateHospitalAsync(s.Admin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, ownHospital);
        var serviceId = await AdminSessions.CreateServiceAsync(s.Admin, s.HospitalId);
        await s.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId });

        var requests = await ApiClient.ReadJsonAsync(await branchManager.GetAsync("/api/doctor-service-requests"));
        requests.EnumerateArray().Should().NotContain(r => r.GetProperty("doctorId").GetGuid() == s.DoctorId);

        (await branchManager.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/approve")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PostAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}/reject")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { serviceId } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.DeleteAsync($"/api/doctors/{s.DoctorId}/services/{serviceId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Talep hâlâ bekliyor (müdahale edilmedi).
        (await StatusOfAsync(s.Doctor, serviceId)).Should().Be("PENDING");
    }

    [Fact]
    public async Task Gecersiz_durum_filtresi_400()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);

        var response = await admin.GetAsync("/api/doctor-service-requests?status=bekliyor");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
