using Hospital.Application.Features.Hospitals.Commands.CreateHospital;
using Hospital.Application.Features.Hospitals.Commands.DeactivateHospital;
using Hospital.Application.Features.Hospitals.Commands.SetWorkingHours;
using Hospital.Application.Features.Hospitals.Commands.UpdateHospital;
using Hospital.Application.Features.Hospitals.Queries.GetHospital;
using Hospital.Application.Features.Hospitals.Queries.ListHospitals;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Hospitals;

public sealed record CreateHospitalRequest(string Name, string? Address, string? Phone, string TimeZoneId);

public sealed record UpdateHospitalRequest(string Name, string? Address, string? Phone, string TimeZoneId);

public sealed record WorkingHoursRequestItem(DayOfWeek Day, TimeOnly OpenTime, TimeOnly CloseTime);

public sealed record SetWorkingHoursRequest(IReadOnlyList<WorkingHoursRequestItem> Hours);

internal sealed class HospitalEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hospitals").WithTags("Hospitals");

        // Yönetici kapsamındaki şubeler (şube yöneticisi yalnız kendi şubesini görür).
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListHospitalsQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Policies.Admin);

        // Yeni şube eklemek organizasyon kapsamı ister.
        group.MapPost("/", async (CreateHospitalRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new CreateHospitalCommand(request.Name, request.Address, request.Phone, request.TimeZoneId), ct);

                return result.Match(id => Results.Created($"/api/hospitals/{id}", new { id }));
            })
            .RequireAuthorization(Policies.SuperAdmin);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetHospitalQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Policies.Admin);

        group.MapPut("/{id:guid}", async (Guid id, UpdateHospitalRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(
                    new UpdateHospitalCommand(id, request.Name, request.Address, request.Phone, request.TimeZoneId), ct))
                .ToNoContent())
            .RequireAuthorization(Policies.Admin);

        group.MapPut("/{id:guid}/working-hours", async (Guid id, SetWorkingHoursRequest request, ISender sender, CancellationToken ct) =>
            {
                var hours = (request.Hours ?? []).Select(h => new WorkingHoursItem(h.Day, h.OpenTime, h.CloseTime)).ToList();
                return (await sender.Send(new SetWorkingHoursCommand(id, hours), ct)).ToNoContent();
            })
            .RequireAuthorization(Policies.Admin);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeactivateHospitalCommand(id), ct)).ToNoContent())
            .RequireAuthorization(Policies.Admin);
    }
}
