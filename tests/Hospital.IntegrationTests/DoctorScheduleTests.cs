using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Hospital.IntegrationTests;

/// <summary>Doktor uygunluk pencereleri ve izinleri (seed şubesi: Pzt-Cum 08-17, Cmt 09-13, Pazar kapalı).</summary>
public class DoctorScheduleTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<(ApiClient Doctor, Guid DoctorId)> NewDoctorAsync()
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(admin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(admin, hospitalId);
        var (doctorId, email) = await AdminSessions.CreateDoctorAsync(admin, departmentId);
        return (await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password), doctorId);
    }

    private static object Window(DayOfWeek day, string start, string end, DateOnly? validFrom = null, DateOnly? validTo = null) => new
    {
        Day = day,
        StartTime = start,
        EndTime = end,
        ValidFrom = validFrom ?? Today,
        ValidTo = validTo,
    };

    // ---------- Uygunluk ----------

    [Fact]
    public async Task Uygunluk_eklenir_listelenir_guncellenir_silinir()
    {
        var (doctor, _) = await NewDoctorAsync();

        var id = await AdminSessions.AddAvailabilityAsync(doctor, DayOfWeek.Tuesday, "09:00", "12:00");

        var list = await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me/availability"));
        var item = list.EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(id);
        item.GetProperty("day").GetInt32().Should().Be((int)DayOfWeek.Tuesday);
        item.GetProperty("startTime").GetString().Should().StartWith("09:00");

        var update = await doctor.PutAsync($"/api/doctors/me/availability/{id}", Window(DayOfWeek.Wednesday, "10:00", "14:00"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = (await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me/availability"))).EnumerateArray().Single();
        updated.GetProperty("day").GetInt32().Should().Be((int)DayOfWeek.Wednesday);
        updated.GetProperty("endTime").GetString().Should().StartWith("14:00");

        (await doctor.DeleteAsync($"/api/doctors/me/availability/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me/availability"))).GetArrayLength().Should().Be(0);
        (await doctor.DeleteAsync($"/api/doctors/me/availability/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "07:00", "12:00")]   // şube 08:00'de açılır
    [InlineData(DayOfWeek.Monday, "16:00", "18:00")]   // şube 17:00'de kapanır
    [InlineData(DayOfWeek.Sunday, "09:00", "12:00")]   // şube pazar kapalı
    [InlineData(DayOfWeek.Saturday, "12:00", "14:00")] // cumartesi 13:00'te kapanır
    public async Task Sube_saatleri_disindaki_uygunluk_400(DayOfWeek day, string start, string end)
    {
        var (doctor, _) = await NewDoctorAsync();

        var response = await doctor.PostAsync("/api/doctors/me/availability", Window(day, start, end));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("doctor.availability_outside_hours");
    }

    [Fact]
    public async Task Cakisan_uygunluk_409_bitisik_ve_farkli_gun_kabul_edilir()
    {
        var (doctor, _) = await NewDoctorAsync();
        await AdminSessions.AddAvailabilityAsync(doctor, DayOfWeek.Monday, "09:00", "12:00");

        var overlapping = await doctor.PostAsync("/api/doctors/me/availability", Window(DayOfWeek.Monday, "11:00", "14:00"));
        var adjacent = await doctor.PostAsync("/api/doctors/me/availability", Window(DayOfWeek.Monday, "12:00", "15:00"));
        var otherDay = await doctor.PostAsync("/api/doctors/me/availability", Window(DayOfWeek.Tuesday, "09:00", "12:00"));

        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(overlapping)).GetProperty("code").GetString().Should().Be("doctor.availability_overlap");
        adjacent.StatusCode.Should().Be(HttpStatusCode.Created);
        otherDay.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Ayni_saat_ayrik_gecerlilik_donemlerinde_cakismaz()
    {
        var (doctor, _) = await NewDoctorAsync();
        await doctor.PostAsync("/api/doctors/me/availability", Window(DayOfWeek.Monday, "09:00", "12:00", Today, Today.AddDays(30)));

        var later = await doctor.PostAsync("/api/doctors/me/availability", Window(DayOfWeek.Monday, "09:00", "12:00", Today.AddDays(31)));
        var overlappingPeriod = await doctor.PostAsync("/api/doctors/me/availability", Window(DayOfWeek.Monday, "09:00", "12:00", Today.AddDays(30), Today.AddDays(40)));

        later.StatusCode.Should().Be(HttpStatusCode.Created);
        overlappingPeriod.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Guncellemede_baska_pencereyle_cakisma_409_kendisiyle_cakisma_degil()
    {
        var (doctor, _) = await NewDoctorAsync();
        var first = await AdminSessions.AddAvailabilityAsync(doctor, DayOfWeek.Monday, "09:00", "12:00");
        var second = await AdminSessions.AddAvailabilityAsync(doctor, DayOfWeek.Monday, "13:00", "15:00");

        var selfOverlap = await doctor.PutAsync($"/api/doctors/me/availability/{first}", Window(DayOfWeek.Monday, "10:00", "12:30"));
        var conflict = await doctor.PutAsync($"/api/doctors/me/availability/{second}", Window(DayOfWeek.Monday, "12:00", "15:00"));

        selfOverlap.StatusCode.Should().Be(HttpStatusCode.NoContent);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Uygunluk_dogrulama_hatalari_alan_bazli_doner()
    {
        var (doctor, _) = await NewDoctorAsync();

        var response = await doctor.PostAsync("/api/doctors/me/availability", new
        {
            Day = DayOfWeek.Monday,
            StartTime = "12:00",
            EndTime = "09:00",
            ValidFrom = Today,
            ValidTo = Today.AddDays(-5),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ApiClient.ReadJsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("endTime", out _).Should().BeTrue();
        errors.TryGetProperty("validTo", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Doktor_baska_doktorun_uygunlugunu_goremez_degistiremez_404()
    {
        var (owner, _) = await NewDoctorAsync();
        var (intruder, _) = await NewDoctorAsync();
        var id = await AdminSessions.AddAvailabilityAsync(owner);

        (await intruder.PutAsync($"/api/doctors/me/availability/{id}", Window(DayOfWeek.Monday, "09:00", "10:00"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.DeleteAsync($"/api/doctors/me/availability/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiClient.ReadJsonAsync(await intruder.GetAsync("/api/doctors/me/availability"))).GetArrayLength().Should().Be(0);
        (await ApiClient.ReadJsonAsync(await owner.GetAsync("/api/doctors/me/availability"))).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Veritabani_uygunluk_cakismasini_uygulama_kontrolu_olmadan_da_engeller()
    {
        var (_, doctorId) = await NewDoctorAsync();

        async Task InsertAsync(string start, string end)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO doctor_availability (id, doctor_id, day_of_week, start_time, end_time, valid_from, valid_to)
                VALUES ({0}, {1}, 1, {2}::time, {3}::time, CURRENT_DATE, NULL)
                """,
                Guid.NewGuid(), doctorId, start, end);
        }

        await InsertAsync("09:00", "12:00");

        var act = () => InsertAsync("11:00", "13:00");

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be("23P01");
        await InsertAsync("12:00", "13:00"); // bitişik pencere serbest
    }

    // ---------- İzin ----------

    [Fact]
    public async Task Izin_eklenir_listelenir_silinir()
    {
        var (doctor, _) = await NewDoctorAsync();
        var start = DateTimeOffset.UtcNow.AddDays(10);

        var create = await doctor.PostAsync("/api/doctors/me/time-off", new { StartAt = start, EndAt = start.AddDays(2), Reason = "Yıllık izin" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ApiClient.ReadJsonAsync(create)).GetProperty("id").GetGuid();

        var list = await ApiClient.ReadJsonAsync(await doctor.GetAsync("/api/doctors/me/time-off"));
        var item = list.EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(id);
        item.GetProperty("reason").GetString().Should().Be("Yıllık izin");

        (await doctor.DeleteAsync($"/api/doctors/me/time-off/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await doctor.DeleteAsync($"/api/doctors/me/time-off/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cakisan_izin_409_bitisik_izin_kabul_edilir()
    {
        var (doctor, _) = await NewDoctorAsync();
        var start = DateTimeOffset.UtcNow.AddDays(10);
        await doctor.PostAsync("/api/doctors/me/time-off", new { StartAt = start, EndAt = start.AddDays(2) });

        var overlapping = await doctor.PostAsync("/api/doctors/me/time-off", new { StartAt = start.AddDays(1), EndAt = start.AddDays(3) });
        var adjacent = await doctor.PostAsync("/api/doctors/me/time-off", new { StartAt = start.AddDays(2), EndAt = start.AddDays(3) });

        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(overlapping)).GetProperty("code").GetString().Should().Be("doctor.time_off_overlap");
        adjacent.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Gecmiste_biten_izin_ve_ters_aralik_400()
    {
        var (doctor, _) = await NewDoctorAsync();
        var now = DateTimeOffset.UtcNow;

        var past = await doctor.PostAsync("/api/doctors/me/time-off", new { StartAt = now.AddDays(-3), EndAt = now.AddDays(-1) });
        var reversed = await doctor.PostAsync("/api/doctors/me/time-off", new { StartAt = now.AddDays(5), EndAt = now.AddDays(4) });

        past.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(past)).GetProperty("code").GetString().Should().Be("doctor.time_off_in_past");
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(reversed)).GetProperty("errors").TryGetProperty("endAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Doktor_baska_doktorun_iznini_silemez_404()
    {
        var (owner, _) = await NewDoctorAsync();
        var (intruder, _) = await NewDoctorAsync();
        var start = DateTimeOffset.UtcNow.AddDays(10);
        var id = (await ApiClient.ReadJsonAsync(
            await owner.PostAsync("/api/doctors/me/time-off", new { StartAt = start, EndAt = start.AddDays(1) }))).GetProperty("id").GetGuid();

        (await intruder.DeleteAsync($"/api/doctors/me/time-off/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiClient.ReadJsonAsync(await owner.GetAsync("/api/doctors/me/time-off"))).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Veritabani_izin_cakismasini_uygulama_kontrolu_olmadan_da_engeller()
    {
        var (_, doctorId) = await NewDoctorAsync();
        var start = DateTimeOffset.UtcNow.AddDays(10);

        async Task InsertAsync(DateTimeOffset startAt, DateTimeOffset endAt)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO doctor_time_off (id, doctor_id, start_at, end_at) VALUES ({0}, {1}, {2}, {3})",
                Guid.NewGuid(), doctorId, startAt, endAt);
        }

        await InsertAsync(start, start.AddDays(2));

        var act = () => InsertAsync(start.AddDays(1), start.AddDays(3));

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be("23P01");
    }
}
