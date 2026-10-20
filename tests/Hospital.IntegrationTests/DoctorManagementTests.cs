using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>Yönetici ile doktor hesabı açma, kapsam denetimi ve rol sınırları.</summary>
public class DoctorManagementTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static object DoctorBody(Guid departmentId, string? email = null, string? diploma = null) => new
    {
        DepartmentId = departmentId,
        Email = email ?? $"doktor.{Guid.NewGuid():N}@test.local",
        Password = TestDoctor.Password,
        FirstName = "Mehmet",
        LastName = "Demir",
        Specialty = "Kardiyoloji",
        DiplomaNo = diploma ?? $"D-{Guid.NewGuid():N}",
        Phone = "05551112233",
    };

    [Fact]
    public async Task Yonetici_doktor_olusturur_doktor_giris_yapip_profilini_gorur()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);

        var (doctorId, email) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);

        var me = await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me"));
        me.GetProperty("id").GetGuid().Should().Be(doctorId);
        me.GetProperty("hospitalId").GetGuid().Should().Be(hospitalId);
        me.GetProperty("departmentId").GetGuid().Should().Be(departmentId);
        me.GetProperty("email").GetString().Should().Be(email);
        me.GetProperty("services").GetArrayLength().Should().Be(0);

        var role = await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/auth/me"));
        role.GetProperty("role").GetString().Should().Be("DOCTOR");
    }

    [Fact]
    public async Task Ayni_eposta_veya_diploma_ikinci_doktorda_409()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var email = $"tekrar.{Guid.NewGuid():N}@test.local";
        var diploma = $"D-{Guid.NewGuid():N}";
        (await superAdmin.PostAsync("/api/doctors", DoctorBody(departmentId, email, diploma))).StatusCode.Should().Be(HttpStatusCode.Created);

        var sameEmail = await superAdmin.PostAsync("/api/doctors", DoctorBody(departmentId, email));
        var sameDiploma = await superAdmin.PostAsync("/api/doctors", DoctorBody(departmentId, diploma: diploma));

        sameEmail.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(sameEmail)).GetProperty("code").GetString().Should().Be("auth.email_taken");
        sameDiploma.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(sameDiploma)).GetProperty("code").GetString().Should().Be("doctor.diploma_taken");
    }

    [Fact]
    public async Task Doktor_olusturmada_alan_bazli_dogrulama_hatalari_doner()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);

        var response = await superAdmin.PostAsync("/api/doctors", new
        {
            DepartmentId = Guid.Empty,
            Email = "eposta-degil",
            Password = "kisa",
            FirstName = "",
            LastName = "Demir",
            Specialty = "Kardiyoloji",
            DiplomaNo = "D-1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ApiClient.ReadJsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("departmentId", out _).Should().BeTrue();
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("password", out _).Should().BeTrue();
        errors.TryGetProperty("firstName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Pasif_veya_olmayan_bolume_doktor_acilamaz()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        (await superAdmin.PostAsync($"/api/departments/{departmentId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var inactive = await superAdmin.PostAsync("/api/doctors", DoctorBody(departmentId));
        var missing = await superAdmin.PostAsync("/api/doctors", DoctorBody(Guid.NewGuid()));

        inactive.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(inactive)).GetProperty("code").GetString().Should().Be("doctor.department_invalid");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sube_yoneticisi_yalniz_kendi_subesine_doktor_ekler_ve_gorur_baska_sube_404()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var ownDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, own);
        var otherDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, other);
        var (otherDoctorId, _) = await AdminSessions.CreateDoctorAsync(superAdmin, otherDepartment);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        (await branchManager.PostAsync("/api/doctors", DoctorBody(otherDepartment))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.GetAsync($"/api/doctors/{otherDoctorId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PutAsync($"/api/doctors/{otherDoctorId}", new
        {
            DepartmentId = otherDepartment,
            FirstName = "Ele",
            LastName = "Geçirildi",
            Specialty = "X",
            DiplomaNo = "X-1",
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.GetAsync($"/api/doctors?hospitalId={other}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var created = await branchManager.PostAsync("/api/doctors", DoctorBody(ownDepartment));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await ApiClient.ReadJsonAsync(await branchManager.GetAsync("/api/doctors"));
        list.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("hospitalId").GetGuid().Should().Be(own);
    }

    [Fact]
    public async Task Yonetici_doktoru_gunceller_baska_subenin_bolumune_tasiyamaz()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalA = await AdminSessions.CreateHospitalAsync(superAdmin);
        var hospitalB = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentA1 = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalA);
        var departmentA2 = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalA);
        var departmentB = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalB);
        var (doctorId, _) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentA1);

        var move = await superAdmin.PutAsync($"/api/doctors/{doctorId}", new
        {
            DepartmentId = departmentA2,
            FirstName = "Yeni",
            LastName = "Ad",
            Specialty = "Nöroloji",
            DiplomaNo = $"D-{Guid.NewGuid():N}",
            Phone = (string?)null,
        });
        var crossHospital = await superAdmin.PutAsync($"/api/doctors/{doctorId}", new
        {
            DepartmentId = departmentB,
            FirstName = "Yeni",
            LastName = "Ad",
            Specialty = "Nöroloji",
            DiplomaNo = $"D-{Guid.NewGuid():N}",
        });

        move.StatusCode.Should().Be(HttpStatusCode.NoContent);
        crossHospital.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var detail = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/doctors/{doctorId}"));
        detail.GetProperty("departmentId").GetGuid().Should().Be(departmentA2);
        detail.GetProperty("firstName").GetString().Should().Be("Yeni");
        detail.GetProperty("specialty").GetString().Should().Be("Nöroloji");
    }

    [Fact]
    public async Task Doktor_kendi_profilinde_yalniz_uzmanlik_ve_telefonu_degistirir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var (_, email) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);

        var update = await doctor.PutAsync("/api/doctors/me", new { Specialty = "Gastroenteroloji", Phone = "05559998877" });
        var invalid = await doctor.PutAsync("/api/doctors/me", new { Specialty = "", Phone = (string?)null });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var me = await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me"));
        me.GetProperty("specialty").GetString().Should().Be("Gastroenteroloji");
        me.GetProperty("phone").GetString().Should().Be("05559998877");
        me.GetProperty("firstName").GetString().Should().Be("Mehmet");
    }

    [Theory]
    [InlineData("/api/doctors")]
    [InlineData("/api/staff")]
    [InlineData("/api/admins")]
    [InlineData("/api/doctor-service-requests")]
    public async Task Hasta_ve_doktor_yonetici_uclarina_giremez_403_anonim_401(string url)
    {
        var anonymous = fixture.CreateClient();
        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var (_, email) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);

        (await anonymous.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await patient.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await doctor.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doktor_hasta_ucuna_hasta_doktor_ucuna_giremez_403()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var (_, email) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);
        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        (await doctor.GetAsync("/api/patients/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await patient.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await patient.GetAsync("/api/doctors/me/availability")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await patient.GetAsync("/api/doctors/me/time-off")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await patient.GetAsync("/api/doctors/me/services")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await superAdmin.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
