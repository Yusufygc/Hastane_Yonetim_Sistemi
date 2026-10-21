using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>
/// Randevu testleri için hazır senaryo: seed şubesinde bölüm + hizmet + doktor, doktor Pazartesi 09:00-12:00 uygun ve
/// hizmeti yönetici tarafından onaylı. Slotlar bir sonraki Pazartesi'dedir (hazırlık süresinden uzak, ufuk içinde).
/// </summary>
public sealed record BookingSetup(
    ApiClient Admin,
    Guid HospitalId,
    Guid DepartmentId,
    Guid DoctorId,
    string DoctorEmail,
    ApiClient Doctor,
    Guid ServiceId,
    DateOnly Date)
{
    /// <summary>Şubenin (Europe/Istanbul, UTC+3) yerel saatinin UTC karşılığı.</summary>
    public DateTimeOffset LocalToUtc(int hour, int minute = 0) =>
        new(Date.ToDateTime(new TimeOnly(hour, minute)).AddHours(-3), TimeSpan.Zero);
}

public static class AppointmentKit
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>En az 2 gün sonraki ilk Pazartesi (şubenin yerel takvimi).</summary>
    public static DateOnly NextMonday(int minDaysAhead = 2)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Istanbul).DateTime);
        var date = today.AddDays(minDaysAhead);
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    public static async Task<BookingSetup> NewSetupAsync(ApiFixture fixture, int serviceDurationMinutes = 30, Guid? hospitalId = null)
    {
        var admin = await AdminSessions.SuperAdminAsync(fixture);
        var hospital = hospitalId ?? await AdminSessions.SeededHospitalIdAsync(admin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(admin, hospital);
        var (doctorId, email) = await AdminSessions.CreateDoctorAsync(admin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, email, TestDoctor.Password);
        var serviceId = await AdminSessions.CreateServiceAsync(admin, hospital, departmentId, durationMinutes: serviceDurationMinutes);

        var assign = await admin.PutAsync($"/api/doctors/{doctorId}/services", new { ServiceIds = new[] { serviceId } });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AdminSessions.AddAvailabilityAsync(doctor, DayOfWeek.Monday, "09:00", "12:00");

        return new BookingSetup(admin, hospital, departmentId, doctorId, email, doctor, serviceId, NextMonday());
    }

    public static async Task<ApiClient> NewPatientAsync(ApiFixture fixture, HospitalApiFactory? factory = null)
    {
        var client = factory is null ? fixture.CreateClient() : new ApiClient(factory.CreateClient());
        await client.RegisterAndLoginAsync();
        return client;
    }

    public static async Task<JsonElement> SlotsAsync(ApiFixture fixture, Guid doctorId, Guid serviceId, DateOnly date)
    {
        var response = await fixture.CreateClient().GetAsync(
            $"/api/catalog/doctors/{doctorId}/slots?serviceId={serviceId}&date={date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("slots");
    }

    public static Task<HttpResponseMessage> BookAsync(ApiClient patient, BookingSetup setup, DateTimeOffset startAt, string? note = null) =>
        patient.PostAsync("/api/appointments", new
        {
            DoctorId = setup.DoctorId,
            ServiceId = setup.ServiceId,
            StartAt = startAt,
            PatientNote = note,
        });

    /// <summary>Randevu oluşturur (201 bekler) ve kimliğini döner.</summary>
    public static async Task<Guid> BookOkAsync(ApiClient patient, BookingSetup setup, int hour = 9, int minute = 0)
    {
        var response = await BookAsync(patient, setup, setup.LocalToUtc(hour, minute));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    public static async Task<string> StatusOfAsync(ApiClient client, Guid appointmentId)
    {
        var response = await client.GetAsync($"/api/appointments/{appointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("status").GetString()!;
    }

    public static async Task<string> CodeOfAsync(HttpResponseMessage response) =>
        (await ApiClient.ReadJsonAsync(response)).GetProperty("code").GetString()!;

    /// <summary>Randevuyu geçmişe taşır (gerçek saatle "başlamış randevu" senaryosu için).</summary>
    public static async Task MoveToPastAsync(ApiFixture fixture, Guid appointmentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var start = DateTimeOffset.UtcNow.AddHours(-2);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE appointment SET start_at = {0}, end_at = {1} WHERE id = {2}",
            start, start.AddMinutes(30), appointmentId);
    }
}
