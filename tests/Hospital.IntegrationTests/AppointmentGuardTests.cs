using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// Faz 3'ten ertelenen denetimler: gelecekte aktif (bekleyen/onaylı) randevusu olan hizmet, izin aralığı ve kullanıcı
/// hesabı üzerindeki yönetici işlemleri engellenir; randevular iptal edilince işlem serbest kalır.
/// </summary>
public class AppointmentGuardTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Gelecekte_aktif_randevusu_olan_hizmet_doktordan_kaldirilamaz_iptalden_sonra_kaldirilir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var remove = await setup.Admin.DeleteAsync($"/api/doctors/{setup.DoctorId}/services/{setup.ServiceId}");
        var replaceWithEmpty = await setup.Admin.PutAsync($"/api/doctors/{setup.DoctorId}/services", new { ServiceIds = Array.Empty<Guid>() });

        remove.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(remove)).Should().Be("doctor.service_has_future_appointments");
        replaceWithEmpty.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(replaceWithEmpty)).Should().Be("doctor.service_has_future_appointments");

        // Hizmet hâlâ doktorda (işlem geri alındı).
        (await ApiClient.ReadJsonAsync(await setup.Doctor.GetAsync("/api/doctors/me/services"))).GetArrayLength().Should().Be(1);

        await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Vazgeçtim" });

        (await setup.Admin.DeleteAsync($"/api/doctors/{setup.DoctorId}/services/{setup.ServiceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Baska_hizmetin_randevusu_bu_hizmetin_kaldirilmasini_engellemez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var otherService = await AdminSessions.CreateServiceAsync(setup.Admin, setup.HospitalId, setup.DepartmentId);
        await setup.Admin.PutAsync($"/api/doctors/{setup.DoctorId}/services", new { ServiceIds = new[] { setup.ServiceId, otherService } });
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        await AppointmentKit.BookOkAsync(patient, setup);

        var removeUnrelated = await setup.Admin.DeleteAsync($"/api/doctors/{setup.DoctorId}/services/{otherService}");

        removeUnrelated.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Aktif_randevu_olan_araliga_izin_eklenemez_randevu_olmayan_araliga_eklenir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        await AppointmentKit.BookOkAsync(patient, setup, 10, 0); // 10:00-10:30

        var overlapping = await setup.Doctor.PostAsync("/api/doctors/me/time-off", new
        {
            StartAt = setup.LocalToUtc(9, 0),
            EndAt = setup.LocalToUtc(11, 0),
        });
        var adjacent = await setup.Doctor.PostAsync("/api/doctors/me/time-off", new
        {
            StartAt = setup.LocalToUtc(10, 30),
            EndAt = setup.LocalToUtc(12, 0),
        });

        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(overlapping)).Should().Be("doctor.time_off_has_appointments");
        adjacent.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Iptal_edilmis_randevu_izin_eklemeyi_engellemez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup, 10, 0);
        await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Vazgeçtim" });

        var response = await setup.Doctor.PostAsync("/api/doctors/me/time-off", new
        {
            StartAt = setup.LocalToUtc(9, 0),
            EndAt = setup.LocalToUtc(11, 0),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Gelecekte_aktif_randevusu_olan_doktor_ve_hasta_pasiflestirilemez_iptalden_sonra_pasiflesir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var patientId = (await ApiClient.ReadJsonAsync(await patient.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var doctorBlocked = await setup.Admin.PostAsync($"/api/users/{setup.DoctorId}/deactivate");
        var patientBlocked = await setup.Admin.PostAsync($"/api/users/{patientId}/deactivate");

        doctorBlocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(doctorBlocked)).Should().Be("user.has_future_appointments");
        patientBlocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Engellenen işlem oturumu düşürmemiş olmalı.
        (await setup.Doctor.GetAsync("/api/doctors/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await patient.GetAsync("/api/patients/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await setup.Admin.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Hesap kapatılıyor" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await setup.Admin.PostAsync($"/api/users/{setup.DoctorId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await setup.Admin.PostAsync($"/api/users/{patientId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Gecmis_ve_tamamlanmis_randevular_pasiflestirmeyi_engellemez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");
        await AppointmentKit.MoveToPastAsync(fixture, id);
        await setup.Doctor.PostAsync($"/api/appointments/{id}/complete");

        var response = await setup.Admin.PostAsync($"/api/users/{setup.DoctorId}/deactivate");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
