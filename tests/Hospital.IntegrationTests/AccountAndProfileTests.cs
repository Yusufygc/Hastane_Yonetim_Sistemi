using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>Hasta profili, parola değiştirme, personel/yönetici hesapları ve hesap pasifleştirme.</summary>
public class AccountAndProfileTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    // ---------- Hasta profili ----------

    [Fact]
    public async Task Hasta_profilinde_TC_maskeli_gorunur_ve_guncellenir()
    {
        var patient = fixture.CreateClient();
        var account = await patient.RegisterAndLoginAsync();

        var profile = await ApiClient.ReadJsonAsync(await patient.GetAsync("/api/patients/me"));
        var masked = profile.GetProperty("maskedIdentityNumber").GetString()!;
        masked.Should().HaveLength(11);
        masked.Should().StartWith(account.IdentityNumber[..3]).And.EndWith(account.IdentityNumber[^2..]);
        masked.Should().Contain("******");
        profile.TryGetProperty("identityNumber", out _).Should().BeFalse();
        profile.GetProperty("email").GetString().Should().Be(account.Email);

        var update = await patient.PutAsync("/api/patients/me", new { FirstName = "Fatma", LastName = "Demir", Phone = "05559998877", BloodType = "ab+" });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ApiClient.ReadJsonAsync(update);
        updated.GetProperty("firstName").GetString().Should().Be("Fatma");
        updated.GetProperty("bloodType").GetString().Should().Be("AB+");
        updated.GetProperty("maskedIdentityNumber").GetString().Should().Be(masked);

        var reread = await ApiClient.ReadJsonAsync(await patient.GetAsync("/api/patients/me"));
        reread.GetProperty("lastName").GetString().Should().Be("Demir");
    }

    [Fact]
    public async Task Hasta_profilinde_gecersiz_kan_grubu_ve_bos_ad_alan_bazli_400()
    {
        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        var response = await patient.PutAsync("/api/patients/me", new { FirstName = "", LastName = "Demir", BloodType = "C+" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ApiClient.ReadJsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("firstName", out _).Should().BeTrue();
        errors.TryGetProperty("bloodType", out _).Should().BeTrue();
    }

    // ---------- Parola değiştirme ----------

    [Fact]
    public async Task Parola_degisince_eski_oturumlar_duser_yeni_parolayla_giris_calisir()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();
        var oldRefresh = client.RefreshToken!;
        const string newPassword = "Yeni1234x";

        var change = await client.PostAsync("/api/auth/change-password", new { CurrentPassword = account.Password, NewPassword = newPassword });

        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Güvenlik damgası döndü: eldeki erişim belirteci artık geçersiz; yenileme belirteçleri iptal edildi.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fixture.CreateClient().RefreshAsync(oldRefresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var oldLogin = await fixture.CreateClient().PostAsync("/api/auth/login", account.LoginBody());
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var fresh = fixture.CreateClient();
        await fresh.LoginAsync(account.Email, newPassword);
        (await fresh.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Parola_degistirmede_yanlis_mevcut_parola_ve_ayni_parola_alan_bazli_400()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        var wrong = await client.PostAsync("/api/auth/change-password", new { CurrentPassword = "Yanlis1234x", NewPassword = "Yeni1234x" });
        var same = await client.PostAsync("/api/auth/change-password", new { CurrentPassword = account.Password, NewPassword = account.Password });
        var weak = await client.PostAsync("/api/auth/change-password", new { CurrentPassword = account.Password, NewPassword = "zayif" });

        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(wrong)).GetProperty("errors").TryGetProperty("currentPassword", out _).Should().BeTrue();
        same.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(same)).GetProperty("code").GetString().Should().Be("auth.password_unchanged");
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Başarısız denemeler oturumu etkilemez.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Parola_degistirme_giris_ister_401()
    {
        var response = await fixture.CreateClient().PostAsync("/api/auth/change-password", new { CurrentPassword = "a", NewPassword = "b" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- Personel ----------

    [Fact]
    public async Task Yonetici_personel_olusturur_listeler_personel_giris_yapar()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var email = $"personel.{Guid.NewGuid():N}@test.local";

        var create = await superAdmin.PostAsync("/api/staff", new
        {
            HospitalId = hospitalId,
            DepartmentId = departmentId,
            Email = email,
            Password = TestAccount.ValidPassword,
            FirstName = "Mehmet",
            LastName = "Kaya",
            Title = "Hemşire",
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var staffId = (await ApiClient.ReadJsonAsync(create)).GetProperty("id").GetGuid();

        var list = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync($"/api/staff?hospitalId={hospitalId}"));
        list.EnumerateArray().Should().ContainSingle(s => s.GetProperty("id").GetGuid() == staffId
                                                          && s.GetProperty("title").GetString() == "Hemşire");

        var staff = await AdminSessions.LoginAsync(fixture, email, TestAccount.ValidPassword);
        (await ApiClient.ReadJsonAsync(await staff.GetAsync("/api/auth/me"))).GetProperty("role").GetString().Should().Be("STAFF");

        // Personel yönetici/doktor/hasta uçlarına giremez.
        (await staff.GetAsync("/api/staff")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync("/api/patients/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Personel_olusturmada_kapsam_ve_bolum_dogrulamasi()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var otherDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, other);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        object Body(Guid hospitalId, Guid? departmentId = null) => new
        {
            HospitalId = hospitalId,
            DepartmentId = departmentId,
            Email = $"personel.{Guid.NewGuid():N}@test.local",
            Password = TestAccount.ValidPassword,
            FirstName = "Mehmet",
            LastName = "Kaya",
            Title = "Teknisyen",
        };

        (await branchManager.PostAsync("/api/staff", Body(other))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.GetAsync($"/api/staff?hospitalId={other}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PostAsync("/api/staff", Body(own, otherDepartment))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await branchManager.PostAsync("/api/staff", Body(own))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Ayni_eposta_ile_ikinci_personel_409()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var body = new
        {
            HospitalId = hospitalId,
            DepartmentId = (Guid?)null,
            Email = $"personel.{Guid.NewGuid():N}@test.local",
            Password = TestAccount.ValidPassword,
            FirstName = "Mehmet",
            LastName = "Kaya",
            Title = "Hemşire",
        };
        (await superAdmin.PostAsync("/api/staff", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await superAdmin.PostAsync("/api/staff", body);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------- Yönetici hesapları ----------

    [Fact]
    public async Task Super_yonetici_sube_yoneticisi_olusturur_yonetici_kendi_subesinin_yoneticilerini_gorur()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var email = $"mudur.{Guid.NewGuid():N}@test.local";

        var create = await superAdmin.PostAsync("/api/admins", new
        {
            AccessLevel = "BRANCH_MANAGER",
            HospitalId = hospitalId,
            Email = email,
            Password = TestAccount.ValidPassword,
            FirstName = "Ali",
            LastName = "Veli",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var manager = await AdminSessions.LoginAsync(fixture, email, TestAccount.ValidPassword);
        var visible = await ApiClient.ReadJsonAsync(await manager.GetAsync("/api/admins"));
        visible.EnumerateArray().Should().OnlyContain(a => a.GetProperty("hospitalId").GetGuid() == hospitalId);
        visible.EnumerateArray().Should().ContainSingle(a => a.GetProperty("email").GetString() == email);

        var all = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync("/api/admins"));
        all.EnumerateArray().Should().Contain(a => a.GetProperty("accessLevel").GetString() == "SUPER_ADMIN");
        all.EnumerateArray().Should().Contain(a => a.GetProperty("email").GetString() == email);
    }

    [Fact]
    public async Task Sube_yoneticisi_yonetici_olusturamaz_403()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, hospitalId);

        var response = await branchManager.PostAsync("/api/admins", new
        {
            AccessLevel = "SUPER_ADMIN",
            Email = $"x.{Guid.NewGuid():N}@test.local",
            Password = TestAccount.ValidPassword,
            FirstName = "Ali",
            LastName = "Veli",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Yonetici_olusturmada_seviye_ve_sube_kurallari_dogrulanir()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(superAdmin);

        object Body(string level, Guid? hospital) => new
        {
            AccessLevel = level,
            HospitalId = hospital,
            Email = $"y.{Guid.NewGuid():N}@test.local",
            Password = TestAccount.ValidPassword,
            FirstName = "Ali",
            LastName = "Veli",
        };

        (await superAdmin.PostAsync("/api/admins", Body("ROOT", null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await superAdmin.PostAsync("/api/admins", Body("BRANCH_MANAGER", null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await superAdmin.PostAsync("/api/admins", Body("SUPER_ADMIN", hospitalId))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await superAdmin.PostAsync("/api/admins", Body("BRANCH_MANAGER", Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await superAdmin.PostAsync("/api/admins", Body("SUPER_ADMIN", null))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---------- Hesap pasifleştirme ----------

    [Fact]
    public async Task Pasiflestirilen_doktorun_oturumu_aninda_duser_giris_yapamaz_aktiflestirilince_girer()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);
        var (doctorId, email) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);
        var refresh = doctor.RefreshToken!;
        (await doctor.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await superAdmin.PostAsync($"/api/users/{doctorId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await doctor.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fixture.CreateClient().RefreshAsync(refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fixture.CreateClient().PostAsync("/api/auth/login", new { Email = email, Password = TestDoctor.Password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await superAdmin.PostAsync($"/api/users/{doctorId}/activate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);
        (await again.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Yonetici_kendi_hesabini_pasiflestiremez_409_olmayan_kullanici_404()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var me = await ApiClient.ReadJsonAsync(await superAdmin.GetAsync("/api/auth/me"));
        var myId = me.GetProperty("id").GetGuid();

        var self = await superAdmin.PostAsync($"/api/users/{myId}/deactivate");
        var missing = await superAdmin.PostAsync($"/api/users/{Guid.NewGuid()}/deactivate");

        self.StatusCode.Should().Be(HttpStatusCode.Conflict);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await superAdmin.GetAsync("/api/hospitals")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subenin_doktorunu_ve_hasta_ile_yoneticileri_pasiflestiremez_404()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var own = await AdminSessions.CreateHospitalAsync(superAdmin);
        var other = await AdminSessions.CreateHospitalAsync(superAdmin);
        var ownDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, own);
        var otherDepartment = await AdminSessions.CreateDepartmentAsync(superAdmin, other);
        var (otherDoctorId, otherEmail) = await AdminSessions.CreateDoctorAsync(superAdmin, otherDepartment);
        var (ownDoctorId, _) = await AdminSessions.CreateDoctorAsync(superAdmin, ownDepartment);
        var branchManager = await AdminSessions.BranchManagerAsync(fixture, own);

        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();
        var patientId = (await ApiClient.ReadJsonAsync(await patient.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();
        var superAdminId = (await ApiClient.ReadJsonAsync(await superAdmin.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();

        (await branchManager.PostAsync($"/api/users/{otherDoctorId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PostAsync($"/api/users/{patientId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await branchManager.PostAsync($"/api/users/{superAdminId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Müdahale edilmeyen doktor hâlâ giriş yapabilir; kendi şubesindeki doktor pasifleştirilebilir.
        (await AdminSessions.LoginAsync(fixture, otherEmail, TestDoctor.Password)).AccessToken.Should().NotBeNull();
        (await branchManager.PostAsync($"/api/users/{ownDoctorId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Hasta_ve_doktor_pasiflestirme_ucuna_giremez_403()
    {
        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        (await patient.PostAsync($"/api/users/{Guid.NewGuid()}/deactivate")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await patient.PostAsync($"/api/users/{Guid.NewGuid()}/activate")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fixture.CreateClient().PostAsync($"/api/users/{Guid.NewGuid()}/deactivate")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
