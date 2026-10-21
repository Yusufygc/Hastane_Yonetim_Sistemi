using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>Anonim slot sorgusu: GET /api/catalog/doctors/{id}/slots (seed şubesi: Pzt-Cum 08-17, Europe/Istanbul).</summary>
public class AppointmentSlotTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static List<DateTimeOffset> Starts(System.Text.Json.JsonElement slots) =>
        slots.EnumerateArray().Select(s => s.GetProperty("startAt").GetDateTimeOffset()).ToList();

    [Fact]
    public async Task Onayli_hizmet_icin_otuz_dakikalik_slotlar_izgarada_uretilir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture, serviceDurationMinutes: 30);

        var response = await fixture.CreateClient().GetAsync(
            $"/api/catalog/doctors/{setup.DoctorId}/slots?serviceId={setup.ServiceId}&date={setup.Date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.ReadJsonAsync(response);
        body.GetProperty("timeZoneId").GetString().Should().Be("Europe/Istanbul");
        body.GetProperty("durationMinutes").GetInt32().Should().Be(30);

        var slots = body.GetProperty("slots");
        var starts = Starts(slots);
        starts.Should().HaveCount(11); // 09:00 … 11:30, 15 dk ızgara
        starts.First().Should().Be(setup.LocalToUtc(9, 0));
        starts.Last().Should().Be(setup.LocalToUtc(11, 30));
        slots[0].GetProperty("localStart").GetString().Should().StartWith("09:00");
        slots[0].GetProperty("endAt").GetDateTimeOffset().Should().Be(setup.LocalToUtc(9, 30));
    }

    [Theory]
    [InlineData(15, 12)]
    [InlineData(60, 9)]
    public async Task Slot_sayisi_hizmet_suresine_gore_degisir(int duration, int expected)
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture, serviceDurationMinutes: duration);

        var slots = await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date);

        slots.GetArrayLength().Should().Be(expected);
    }

    [Fact]
    public async Task Onaylanmamis_hizmet_icin_slot_yok_400()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var pendingService = await AdminSessions.CreateServiceAsync(setup.Admin, setup.HospitalId, setup.DepartmentId);
        (await setup.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = pendingService })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var response = await fixture.CreateClient().GetAsync(
            $"/api/catalog/doctors/{setup.DoctorId}/slots?serviceId={pendingService}&date={setup.Date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AppointmentKit.CodeOfAsync(response)).Should().Be("appointment.service_not_offered");
    }

    [Fact]
    public async Task Olmayan_doktor_404_baska_subenin_hizmeti_400_serviceId_eksikse_400()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var otherHospital = await AdminSessions.CreateHospitalAsync(setup.Admin);
        var foreignService = await AdminSessions.CreateServiceAsync(setup.Admin, otherHospital);
        var anonymous = fixture.CreateClient();
        var date = $"{setup.Date:yyyy-MM-dd}";

        var unknownDoctor = await anonymous.GetAsync($"/api/catalog/doctors/{Guid.NewGuid()}/slots?serviceId={setup.ServiceId}&date={date}");
        var foreign = await anonymous.GetAsync($"/api/catalog/doctors/{setup.DoctorId}/slots?serviceId={foreignService}&date={date}");
        var noService = await anonymous.GetAsync($"/api/catalog/doctors/{setup.DoctorId}/slots?date={date}");
        var badDate = await anonymous.GetAsync($"/api/catalog/doctors/{setup.DoctorId}/slots?serviceId={setup.ServiceId}&date=02.11.2026");
        var noDate = await anonymous.GetAsync($"/api/catalog/doctors/{setup.DoctorId}/slots?serviceId={setup.ServiceId}");

        unknownDoctor.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AppointmentKit.CodeOfAsync(unknownDoctor)).Should().Be("appointment.doctor_not_found");
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AppointmentKit.CodeOfAsync(foreign)).Should().Be("appointment.service_invalid");
        noService.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badDate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(badDate)).GetProperty("errors").TryGetProperty("date", out _).Should().BeTrue();
        noDate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pasif_doktor_icin_slot_yok_404()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        (await setup.Admin.PostAsync($"/api/users/{setup.DoctorId}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await fixture.CreateClient().GetAsync(
            $"/api/catalog/doctors/{setup.DoctorId}/slots?serviceId={setup.ServiceId}&date={setup.Date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uygunlugu_olmayan_gunler_bos_doner()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);

        foreach (var offset in new[] { 1, 6 }) // Salı, Pazar
        {
            var slots = await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date.AddDays(offset));
            slots.GetArrayLength().Should().Be(0);
        }
    }

    [Fact]
    public async Task Gecmis_gun_ve_ufuk_disi_tarih_bos_doner()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);

        var past = await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date.AddDays(-14));
        var beyondHorizon = await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date.AddDays(70));

        past.GetArrayLength().Should().Be(0);
        beyondHorizon.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Izin_gunu_slot_uretmez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var response = await setup.Doctor.PostAsync("/api/doctors/me/time-off", new
        {
            StartAt = setup.LocalToUtc(0, 0),
            EndAt = setup.LocalToUtc(0, 0).AddDays(1),
            Reason = "Kongre",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var slots = await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date);

        slots.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Izin_araligi_yalniz_kesisen_slotlari_eler()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        await setup.Doctor.PostAsync("/api/doctors/me/time-off", new
        {
            StartAt = setup.LocalToUtc(10, 0),
            EndAt = setup.LocalToUtc(11, 0),
        });

        var starts = Starts(await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date));

        starts.Should().Contain(setup.LocalToUtc(9, 30)).And.Contain(setup.LocalToUtc(11, 0));
        starts.Should().NotContain(setup.LocalToUtc(9, 45)).And.NotContain(setup.LocalToUtc(10, 30));
    }

    [Fact]
    public async Task Alinan_slot_baskalarina_gorunmez_komsu_slotlar_kalir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        await AppointmentKit.BookOkAsync(patient, setup, 10, 0); // 10:00-10:30

        var starts = Starts(await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date));

        starts.Should().NotContain([setup.LocalToUtc(9, 45), setup.LocalToUtc(10, 0), setup.LocalToUtc(10, 15)]);
        starts.Should().Contain([setup.LocalToUtc(9, 30), setup.LocalToUtc(10, 30)]);
        starts.Should().HaveCount(11 - 3);
    }

    [Fact]
    public async Task Reddedilen_ve_iptal_edilen_randevunun_slotu_yeniden_gorunur()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (p1, p2) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var rejected = await AppointmentKit.BookOkAsync(p1, setup, 9, 0);
        var cancelled = await AppointmentKit.BookOkAsync(p2, setup, 10, 0);

        (await setup.Doctor.PostAsync($"/api/appointments/{rejected}/reject", new { Reason = "Uygun değil" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await p2.PostAsync($"/api/appointments/{cancelled}/cancel", new { Reason = "Vazgeçtim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var starts = Starts(await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, setup.ServiceId, setup.Date));

        starts.Should().HaveCount(11);
        starts.Should().Contain([setup.LocalToUtc(9, 0), setup.LocalToUtc(10, 0)]);
    }

    [Fact]
    public async Task Farkli_sureli_iki_hizmet_ayni_doktorun_gununu_paylasir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture, serviceDurationMinutes: 30);
        var longService = await AdminSessions.CreateServiceAsync(setup.Admin, setup.HospitalId, setup.DepartmentId, durationMinutes: 60);
        await setup.Admin.PutAsync($"/api/doctors/{setup.DoctorId}/services", new { ServiceIds = new[] { setup.ServiceId, longService } });
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        await AppointmentKit.BookOkAsync(patient, setup, 10, 0); // 30 dk'lık: 10:00-10:30

        var longStarts = Starts(await AppointmentKit.SlotsAsync(fixture, setup.DoctorId, longService, setup.Date));

        // 60 dk'lık hizmet 10:00-10:30'a değen başlangıçlardan kaçınır: 09:15 (→10:15) ve 10:15 (→11:15 ... 10:15 başı çakışır) elenir.
        longStarts.Should().Contain(setup.LocalToUtc(9, 0));         // 09:00-10:00 serbest
        longStarts.Should().NotContain(setup.LocalToUtc(9, 15));     // 09:15-10:15 → 10:00'la çakışır
        longStarts.Should().NotContain(setup.LocalToUtc(10, 0));
        longStarts.Should().NotContain(setup.LocalToUtc(10, 15));
        longStarts.Should().Contain(setup.LocalToUtc(10, 30));       // 10:30-11:30 serbest
    }
}
