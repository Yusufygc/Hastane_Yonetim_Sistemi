using Hospital.Application.Features.Appointments.Commands.ApproveAppointment;
using Hospital.Application.Features.Appointments.Commands.CancelAppointment;
using Hospital.Application.Features.Appointments.Commands.CompleteAppointment;
using Hospital.Application.Features.Appointments.Commands.CreateAppointment;
using Hospital.Application.Features.Appointments.Commands.RejectAppointment;
using Hospital.Application.Features.Appointments.Queries.GetAppointmentById;
using Hospital.Application.Features.Appointments.Queries.GetAvailableSlots;
using Hospital.Application.Features.Appointments.Queries.GetDoctorAppointments;
using Hospital.Application.Features.Appointments.Queries.GetDoctorSchedule;
using Hospital.Application.Features.Appointments.Queries.GetMyAppointments;
using Hospital.Application.Features.Appointments.Queries.ListAppointments;
using System.Globalization;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Appointments;

public sealed record CreateAppointmentRequest(Guid DoctorId, Guid ServiceId, DateTimeOffset StartAt, string? PatientNote);

public sealed record AppointmentReasonRequest(string Reason);

internal sealed class AppointmentEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        // Müsait slotlar anonimdir (katalog); randevu almak için giriş gerekir.
        // Tarih yalnızca yyyy-MM-dd kabul edilir: kültüre bağlı ayrıştırma "02.11.2026"yı sessizce 11 Şubat sayabilirdi.
        app.MapGet("/api/catalog/doctors/{id:guid}/slots",
                async (Guid id, Guid serviceId, string? date, ISender sender, CancellationToken ct) =>
                {
                    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                    {
                        return ((Result)Error.Validation(
                            "validation.failed",
                            "Tarih geçersiz.",
                            new Dictionary<string, string[]> { ["date"] = ["Tarih (date) yyyy-MM-dd biçiminde olmalıdır (ör. 2026-11-02)."] })).ToProblem();
                    }

                    return (await sender.Send(new GetAvailableSlotsQuery(id, serviceId, day), ct)).Match(Results.Ok);
                })
            .WithTags("Catalog")
            .AllowAnonymous();

        var group = app.MapGroup("/api/appointments").WithTags("Appointments");

        group.MapPost("/", async (CreateAppointmentRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new CreateAppointmentCommand(request.DoctorId, request.ServiceId, request.StartAt, request.PatientNote), ct);

                return result.Match(id => Results.Created($"/api/appointments/{id}", new { id }));
            })
            .RequireAuthorization(Policies.Patient);

        // Yönetici: kapsamındaki şubelerin randevuları.
        group.MapGet("/", async (Guid? hospitalId, string? status, DateTimeOffset? from, DateTimeOffset? to, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListAppointmentsQuery(hospitalId, status, from, to), ct)).Match(Results.Ok))
            .RequireAuthorization(Policies.Admin);

        // Sahiplik/kapsam handler'da: başkasının randevusu → 404.
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAppointmentByIdQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Policies.Authenticated);

        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ApproveAppointmentCommand(id), ct)).ToNoContent())
            .RequireAuthorization(Policies.Doctor);

        group.MapPost("/{id:guid}/reject", async (Guid id, AppointmentReasonRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RejectAppointmentCommand(id, request.Reason), ct)).ToNoContent())
            .RequireAuthorization(Policies.Doctor);

        group.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CompleteAppointmentCommand(id), ct)).ToNoContent())
            .RequireAuthorization(Policies.Doctor);

        // Hasta / doktor / yönetici iptal edebilir; rol ve sahiplik denetimi handler'da.
        group.MapPost("/{id:guid}/cancel", async (Guid id, AppointmentReasonRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CancelAppointmentCommand(id, request.Reason), ct)).ToNoContent())
            .RequireAuthorization(Policies.Authenticated);

        app.MapGet("/api/patients/me/appointments", async (string? status, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyAppointmentsQuery(status), ct)).Match(Results.Ok))
            .WithTags("Patients")
            .RequireAuthorization(Policies.Patient);

        var doctorGroup = app.MapGroup("/api/doctors/me").WithTags("Doctors (self)").RequireAuthorization(Policies.Doctor);

        doctorGroup.MapGet("/appointments", async (DateTimeOffset? from, DateTimeOffset? to, string? status, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetDoctorAppointmentsQuery(from, to, status), ct)).Match(Results.Ok));

        // Takvim: yalnız onaylı randevular (varsayılan: şimdiden 7 gün).
        doctorGroup.MapGet("/schedule", async (DateTimeOffset? from, DateTimeOffset? to, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetDoctorScheduleQuery(from, to), ct)).Match(Results.Ok));
    }
}
