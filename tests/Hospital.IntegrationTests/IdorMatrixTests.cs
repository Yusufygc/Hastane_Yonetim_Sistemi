using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// IDOR (başkasının kaynağına kimlikle erişim): başka şubenin yöneticisi, bir şubenin tüm yönetim kaynaklarına 404 almalı
/// (varlık sızmaz) ve kaynak <b>değişmemeli</b>. Kurum düzeyi uçlar (organizasyon, yeni şube, yönetici oluşturma) şube yöneticisine 403'tür.
/// </summary>
public class IdorMatrixTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Scene(
        ApiClient SuperAdmin,
        ApiClient ForeignManager,
        Guid HospitalId,
        Guid DepartmentId,
        Guid ServiceId,
        Guid DoctorId,
        Guid StaffId,
        string HospitalName);

    private async Task<Scene> NewSceneAsync()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(admin);
        var foreignHospital = await AdminSessions.CreateHospitalAsync(admin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(admin, hospitalId);
        var serviceId = await AdminSessions.CreateServiceAsync(admin, hospitalId, departmentId);
        var (doctorId, _) = await AdminSessions.CreateDoctorAsync(admin, departmentId);

        var staffResponse = await admin.PostAsync("/api/staff", new
        {
            HospitalId = hospitalId,
            Email = $"personel.{Guid.NewGuid():N}@test.local",
            Password = TestAccount.ValidPassword,
            FirstName = "Mehmet",
            LastName = "Kaya",
            Title = "Hemşire",
        });
        staffResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var hospital = await ApiClient.ReadJsonAsync(await admin.GetAsync($"/api/hospitals/{hospitalId}"));

        return new Scene(
            admin,
            await AdminSessions.BranchManagerAsync(fixture, foreignHospital),
            hospitalId,
            departmentId,
            serviceId,
            doctorId,
            (await ApiClient.ReadJsonAsync(staffResponse)).GetProperty("id").GetGuid(),
            hospital.GetProperty("name").GetString()!);
    }

    [Fact]
    public async Task Baska_subenin_yoneticisi_hicbir_kaynaga_ulasamaz_404_ve_kaynak_degismez()
    {
        var s = await NewSceneAsync();
        var manager = s.ForeignManager;

        var attempts = new (string Name, Func<Task<HttpResponseMessage>> Call)[]
        {
            ("GET şube", () => manager.GetAsync($"/api/hospitals/{s.HospitalId}")),
            ("PUT şube", () => manager.PutAsync($"/api/hospitals/{s.HospitalId}", new { Name = "Ele geçirildi", Address = "x", Phone = "1", TimeZoneId = "Europe/Istanbul" })),
            ("PUT çalışma saati", () => manager.PutAsync($"/api/hospitals/{s.HospitalId}/working-hours", new { Hours = Array.Empty<object>() })),
            ("POST şube pasifleştir", () => manager.PostAsync($"/api/hospitals/{s.HospitalId}/deactivate")),
            ("GET bölümler", () => manager.GetAsync($"/api/hospitals/{s.HospitalId}/departments")),
            ("POST bölüm", () => manager.PostAsync($"/api/hospitals/{s.HospitalId}/departments", new { Name = "Sızma" })),
            ("PUT bölüm", () => manager.PutAsync($"/api/departments/{s.DepartmentId}", new { Name = "Ele geçirildi" })),
            ("POST bölüm pasifleştir", () => manager.PostAsync($"/api/departments/{s.DepartmentId}/deactivate")),
            ("GET hizmetler (şube)", () => manager.GetAsync($"/api/services?hospitalId={s.HospitalId}")),
            ("GET hizmet", () => manager.GetAsync($"/api/services/{s.ServiceId}")),
            ("PUT hizmet", () => manager.PutAsync($"/api/services/{s.ServiceId}", new { DepartmentId = s.DepartmentId, Name = "Ele geçirildi", DurationMinutes = 99, Price = 1m, Currency = "TRY" })),
            ("POST hizmet pasifleştir", () => manager.PostAsync($"/api/services/{s.ServiceId}/deactivate")),
            ("POST hizmet oluştur", () => manager.PostAsync("/api/services", new { HospitalId = s.HospitalId, Name = "Sızma", DurationMinutes = 10, Price = 1m, Currency = "TRY" })),
            ("GET doktor", () => manager.GetAsync($"/api/doctors/{s.DoctorId}")),
            ("PUT doktor", () => manager.PutAsync($"/api/doctors/{s.DoctorId}", new { DepartmentId = s.DepartmentId, FirstName = "X", LastName = "Y", Specialty = "Z", DiplomaNo = "ele-gecirildi" })),
            ("PUT doktor hizmetleri", () => manager.PutAsync($"/api/doctors/{s.DoctorId}/services", new { ServiceIds = new[] { s.ServiceId } })),
            ("POST hizmet onayı", () => manager.PostAsync($"/api/doctors/{s.DoctorId}/services/{s.ServiceId}/approve")),
            ("POST hizmet reddi", () => manager.PostAsync($"/api/doctors/{s.DoctorId}/services/{s.ServiceId}/reject")),
            ("DELETE doktor hizmeti", () => manager.DeleteAsync($"/api/doctors/{s.DoctorId}/services/{s.ServiceId}")),
            ("POST doktor oluştur", () => manager.PostAsync("/api/doctors", new { DepartmentId = s.DepartmentId, Email = $"x.{Guid.NewGuid():N}@test.local", Password = TestAccount.ValidPassword, FirstName = "X", LastName = "Y", Specialty = "Z", DiplomaNo = $"D-{Guid.NewGuid():N}" })),
            ("GET doktorlar (şube)", () => manager.GetAsync($"/api/doctors?hospitalId={s.HospitalId}")),
            ("GET personel (şube)", () => manager.GetAsync($"/api/staff?hospitalId={s.HospitalId}")),
            ("POST personel oluştur", () => manager.PostAsync("/api/staff", new { HospitalId = s.HospitalId, Email = $"x.{Guid.NewGuid():N}@test.local", Password = TestAccount.ValidPassword, FirstName = "X", LastName = "Y", Title = "T" })),
            ("POST doktoru pasifleştir", () => manager.PostAsync($"/api/users/{s.DoctorId}/deactivate")),
            ("POST doktoru etkinleştir", () => manager.PostAsync($"/api/users/{s.DoctorId}/activate")),
            ("POST personeli pasifleştir", () => manager.PostAsync($"/api/users/{s.StaffId}/deactivate")),
            ("GET randevular (şube)", () => manager.GetAsync($"/api/appointments?hospitalId={s.HospitalId}")),
        };

        var failures = new List<string>();
        foreach (var (name, call) in attempts)
        {
            var response = await call();
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                failures.Add($"{name}: 404 beklenirdi, {(int)response.StatusCode} döndü");
            }
        }

        failures.Should().BeEmpty(string.Join("\n", failures));

        // Hiçbir kaynak değişmedi: şube adı, bölüm/hizmet/doktor/personel aktif ve aynı.
        var hospital = await ApiClient.ReadJsonAsync(await s.SuperAdmin.GetAsync($"/api/hospitals/{s.HospitalId}"));
        hospital.GetProperty("name").GetString().Should().Be(s.HospitalName);
        hospital.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var departments = await ApiClient.ReadJsonAsync(await s.SuperAdmin.GetAsync($"/api/hospitals/{s.HospitalId}/departments"));
        departments.EnumerateArray().Should().ContainSingle(d => d.GetProperty("id").GetGuid() == s.DepartmentId && d.GetProperty("isActive").GetBoolean());

        var service = await ApiClient.ReadJsonAsync(await s.SuperAdmin.GetAsync($"/api/services/{s.ServiceId}"));
        service.GetProperty("isActive").GetBoolean().Should().BeTrue();
        service.GetProperty("name").GetString().Should().NotBe("Ele geçirildi");

        var doctor = await ApiClient.ReadJsonAsync(await s.SuperAdmin.GetAsync($"/api/doctors/{s.DoctorId}"));
        doctor.GetProperty("isActive").GetBoolean().Should().BeTrue();
        doctor.GetProperty("firstName").GetString().Should().Be("Mehmet");
        doctor.GetProperty("services").GetArrayLength().Should().Be(0);

        var staff = await ApiClient.ReadJsonAsync(await s.SuperAdmin.GetAsync($"/api/staff?hospitalId={s.HospitalId}"));
        staff.EnumerateArray().Should().ContainSingle(m => m.GetProperty("id").GetGuid() == s.StaffId && m.GetProperty("isActive").GetBoolean());
        staff.GetArrayLength().Should().Be(1); // "Sızma" personeli oluşmadı
    }

    [Fact]
    public async Task Kurum_duzeyi_uclar_sube_yoneticisine_403()
    {
        var s = await NewSceneAsync();
        var manager = s.ForeignManager;

        (await manager.GetAsync("/api/organization")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.PutAsync("/api/organization", new { Name = "Ele geçirildi" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.PostAsync("/api/hospitals", new { Name = "Sızma", TimeZoneId = "Europe/Istanbul" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.PostAsync("/api/admins", new { AccessLevel = "SUPER_ADMIN", Email = "x@test.local", Password = TestAccount.ValidPassword, FirstName = "X", LastName = "Y" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var organization = await ApiClient.ReadJsonAsync(await s.SuperAdmin.GetAsync("/api/organization"));
        organization.GetProperty("name").GetString().Should().NotBe("Ele geçirildi");
    }

    [Fact]
    public async Task Hasta_baska_hastanin_kaynagina_ulasamaz_randevu_ve_profil()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (owner, stranger) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var id = await AppointmentKit.BookOkAsync(owner, setup);

        (await stranger.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Sızma" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await ApiClient.ReadJsonAsync(await stranger.GetAsync("/api/patients/me/appointments"));
        list.GetArrayLength().Should().Be(0);
        (await AppointmentKit.StatusOfAsync(owner, id)).Should().Be("PENDING");
    }

    [Fact]
    public async Task Doktor_baska_doktorun_randevu_ve_uygunlugunu_goremez_degistiremez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var intruder = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        (await intruder.Doctor.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Doctor.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Doctor.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Sızma" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await ApiClient.ReadJsonAsync(await intruder.Doctor.GetAsync("/api/doctors/me/appointments"));
        list.EnumerateArray().Should().NotContain(a => a.GetProperty("id").GetGuid() == id);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("PENDING");
    }
}
