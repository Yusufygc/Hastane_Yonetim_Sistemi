using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// Sayfalama: liste uçları gövdede yalnız diziyi döner; toplam ve sayfa bilgisi <c>X-Total-Count</c>, <c>X-Page</c>,
/// <c>X-Page-Size</c> başlıklarındadır. Sıra deterministiktir (sayfalar arasında kayma/yinelenme olmaz).
/// </summary>
public class PaginationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static string Header(HttpResponseMessage response, string name) => response.Headers.GetValues(name).Single();

    private static List<Guid> Ids(System.Text.Json.JsonElement array, string property = "id") =>
        array.EnumerateArray().Select(item => item.GetProperty(property).GetGuid()).ToList();

    private async Task<(BookingSetup Setup, ApiClient Patient, List<Guid> Appointments)> PatientWithFiveAppointmentsAsync()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var ids = new List<Guid>();

        // 09:00, 09:30, 10:00, 10:30, 11:00 (30 dk; çakışmaz)
        foreach (var (hour, minute) in new[] { (9, 0), (9, 30), (10, 0), (10, 30), (11, 0) })
        {
            ids.Add(await AppointmentKit.BookOkAsync(patient, setup, hour, minute));
        }

        return (setup, patient, ids);
    }

    [Fact]
    public async Task Hasta_randevulari_sayfalanir_baslik_toplami_verir_yeniden_eskiye_siralidir()
    {
        var (_, patient, ids) = await PatientWithFiveAppointmentsAsync();

        var page1 = await patient.GetAsync("/api/patients/me/appointments?page=1&pageSize=2");
        var page2 = await patient.GetAsync("/api/patients/me/appointments?page=2&pageSize=2");
        var page3 = await patient.GetAsync("/api/patients/me/appointments?page=3&pageSize=2");
        var page4 = await patient.GetAsync("/api/patients/me/appointments?page=4&pageSize=2");

        foreach (var response in new[] { page1, page2, page3, page4 })
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            Header(response, "X-Total-Count").Should().Be("5");
            Header(response, "X-Page-Size").Should().Be("2");
        }

        Header(page1, "X-Page").Should().Be("1");
        Header(page3, "X-Page").Should().Be("3");

        var first = Ids(await ApiClient.ReadJsonAsync(page1));
        var second = Ids(await ApiClient.ReadJsonAsync(page2));
        var third = Ids(await ApiClient.ReadJsonAsync(page3));
        var fourth = Ids(await ApiClient.ReadJsonAsync(page4));

        first.Should().HaveCount(2);
        second.Should().HaveCount(2);
        third.Should().HaveCount(1);
        fourth.Should().BeEmpty(); // aralık dışı sayfa boş dizi (hata değil)

        // Yeniden eskiye: 11:00, 10:30 | 10:00, 09:30 | 09:00; sayfalar birleşince tüm kayıtlar bir kez.
        first.Concat(second).Concat(third).Should().Equal(ids.AsEnumerable().Reverse());
    }

    [Fact]
    public async Task Parametresiz_istek_ilk_sayfa_varsayilan_boyut_50()
    {
        var (_, patient, ids) = await PatientWithFiveAppointmentsAsync();

        var response = await patient.GetAsync("/api/patients/me/appointments");

        Header(response, "X-Page").Should().Be("1");
        Header(response, "X-Page-Size").Should().Be("50");
        Header(response, "X-Total-Count").Should().Be("5");
        Ids(await ApiClient.ReadJsonAsync(response)).Should().HaveCount(5);
        ids.Should().HaveCount(5);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    [InlineData("page=0&pageSize=500")]
    public async Task Gecersiz_sayfalama_400_alan_bazli(string query)
    {
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var response = await patient.GetAsync($"/api/patients/me/appointments?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ApiClient.ReadJsonAsync(response)).GetProperty("errors");
        (errors.TryGetProperty("page", out _) || errors.TryGetProperty("pageSize", out _)).Should().BeTrue();
    }

    [Fact]
    public async Task Sayi_olmayan_sayfa_degeri_400()
    {
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var response = await patient.GetAsync("/api/patients/me/appointments?page=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Durum_filtresi_sayfalamayla_birlikte_toplami_filtreye_gore_verir()
    {
        var (setup, patient, ids) = await PatientWithFiveAppointmentsAsync();
        await patient.PostAsync($"/api/appointments/{ids[0]}/cancel", new { Reason = "Vazgeçtim" });
        await patient.PostAsync($"/api/appointments/{ids[1]}/cancel", new { Reason = "Vazgeçtim" });

        var pending = await patient.GetAsync("/api/patients/me/appointments?status=pending&pageSize=2");
        var cancelled = await patient.GetAsync("/api/patients/me/appointments?status=CANCELLED&pageSize=10");

        Header(pending, "X-Total-Count").Should().Be("3");
        Ids(await ApiClient.ReadJsonAsync(pending)).Should().HaveCount(2);
        Header(cancelled, "X-Total-Count").Should().Be("2");
        setup.DoctorId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Doktor_randevulari_eskiden_yeniye_sayfalanir_takvim_sayfalanmaz()
    {
        var (setup, _, ids) = await PatientWithFiveAppointmentsAsync();

        var page1 = await setup.Doctor.GetAsync("/api/doctors/me/appointments?pageSize=3");
        var page2 = await setup.Doctor.GetAsync("/api/doctors/me/appointments?pageSize=3&page=2");
        var schedule = await setup.Doctor.GetAsync("/api/doctors/me/schedule");

        Header(page1, "X-Total-Count").Should().Be("5");
        Ids(await ApiClient.ReadJsonAsync(page1)).Concat(Ids(await ApiClient.ReadJsonAsync(page2))).Should().Equal(ids); // 09:00 → 11:00
        schedule.Headers.Contains("X-Total-Count").Should().BeFalse(); // takvim penceresi sayfalanmaz
    }

    [Fact]
    public async Task Yonetici_randevu_listesi_sayfalanir_kapsam_disi_dahil_edilmez()
    {
        var (setup, _, ids) = await PatientWithFiveAppointmentsAsync();
        var manager = await AdminSessions.BranchManagerAsync(fixture, setup.HospitalId);
        var otherHospital = await AdminSessions.CreateHospitalAsync(setup.Admin);
        var otherManager = await AdminSessions.BranchManagerAsync(fixture, otherHospital);

        var page = await manager.GetAsync($"/api/appointments?hospitalId={setup.HospitalId}&pageSize=2&status=PENDING");
        var foreign = await otherManager.GetAsync("/api/appointments?pageSize=2");

        int.Parse(Header(page, "X-Total-Count")).Should().BeGreaterThanOrEqualTo(ids.Count);
        Ids(await ApiClient.ReadJsonAsync(page)).Should().HaveCount(2);
        Header(foreign, "X-Total-Count").Should().Be("0");
        (await ApiClient.ReadJsonAsync(foreign)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Doktor_listesi_ve_personel_listesi_sayfalanir()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(admin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(admin, hospitalId);
        var doctorIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            doctorIds.Add((await AdminSessions.CreateDoctorAsync(admin, departmentId)).Id);
        }

        for (var i = 0; i < 3; i++)
        {
            var created = await admin.PostAsync("/api/staff", new
            {
                HospitalId = hospitalId,
                Email = $"personel.{Guid.NewGuid():N}@test.local",
                Password = TestAccount.ValidPassword,
                FirstName = "Mehmet",
                LastName = "Kaya",
                Title = "Hemşire",
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var doctors1 = await admin.GetAsync($"/api/doctors?departmentId={departmentId}&pageSize=2");
        var doctors2 = await admin.GetAsync($"/api/doctors?departmentId={departmentId}&pageSize=2&page=2");
        var staff1 = await admin.GetAsync($"/api/staff?hospitalId={hospitalId}&pageSize=2");
        var staff2 = await admin.GetAsync($"/api/staff?hospitalId={hospitalId}&pageSize=2&page=2");

        Header(doctors1, "X-Total-Count").Should().Be("3");
        Header(staff1, "X-Total-Count").Should().Be("3");
        var allDoctors = Ids(await ApiClient.ReadJsonAsync(doctors1)).Concat(Ids(await ApiClient.ReadJsonAsync(doctors2))).ToList();
        allDoctors.Should().BeEquivalentTo(doctorIds).And.OnlyHaveUniqueItems();
        Ids(await ApiClient.ReadJsonAsync(staff1)).Should().HaveCount(2);
        Ids(await ApiClient.ReadJsonAsync(staff2)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Hizmet_talepleri_sayfalanir_yonetici_kapsami_korunur()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.CreateHospitalAsync(admin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(admin, hospitalId);
        var serviceId = await AdminSessions.CreateServiceAsync(admin, hospitalId, departmentId);
        var manager = await AdminSessions.BranchManagerAsync(fixture, hospitalId);

        for (var i = 0; i < 3; i++)
        {
            var (_, email) = await AdminSessions.CreateDoctorAsync(admin, departmentId);
            var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);
            (await doctor.PostAsync("/api/doctors/me/services", new { ServiceId = serviceId })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var page1 = await manager.GetAsync("/api/doctor-service-requests?status=pending&pageSize=2");
        var page2 = await manager.GetAsync("/api/doctor-service-requests?status=pending&pageSize=2&page=2");

        Header(page1, "X-Total-Count").Should().Be("3");
        var all = (await ApiClient.ReadJsonAsync(page1)).EnumerateArray().Concat((await ApiClient.ReadJsonAsync(page2)).EnumerateArray())
            .Select(r => r.GetProperty("doctorId").GetGuid()).ToList();
        all.Should().HaveCount(3).And.OnlyHaveUniqueItems();
    }
}
