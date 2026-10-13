using Hospital.Domain.Entities.Appointments;
using Hospital.Persistence.Context;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hospital.IntegrationTests.Infrastructure;

public sealed record ProbeAppointmentRequest(Guid PatientId, Guid DoctorId, DateTimeOffset StartAt, DateTimeOffset EndAt);

/// <summary>
/// Yalnızca testlerde kayıtlı: veritabanı kısıt ihlalinin HTTP'de 409'a çevrildiğini doğrular.
/// Gerçek CreateAppointment komutu Faz 4'tedir.
/// </summary>
internal sealed class AppointmentProbeEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/test/appointments", async (ProbeAppointmentRequest request, AppDbContext db, CancellationToken ct) =>
            {
                var appointment = Appointment.Request(
                    request.PatientId, request.DoctorId, request.StartAt, request.EndAt, DateTimeOffset.UtcNow);

                db.Appointments.Add(appointment);

                // Kısıt ihlali DataConflictException olarak fırlar; GlobalExceptionHandler 409'a çevirir.
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/test/appointments/{appointment.Id}", new { appointment.Id });
            })
            .RequireAuthorization(Policies.Authenticated);
    }
}
