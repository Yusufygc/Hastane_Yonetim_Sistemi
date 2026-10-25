using Hospital.Application.Features.Doctors;
using Hospital.Application.Features.Doctors.Commands.AddMyAvailability;
using Hospital.Application.Features.Doctors.Commands.AddMyTimeOff;
using Hospital.Application.Features.Doctors.Commands.ApproveDoctorService;
using Hospital.Application.Features.Doctors.Commands.AssignDoctorServices;
using Hospital.Application.Features.Doctors.Commands.CreateDoctor;
using Hospital.Application.Features.Doctors.Commands.RejectDoctorService;
using Hospital.Application.Features.Doctors.Commands.RemoveDoctorService;
using Hospital.Application.Features.Doctors.Commands.RemoveMyAvailability;
using Hospital.Application.Features.Doctors.Commands.RemoveMyService;
using Hospital.Application.Features.Doctors.Commands.RemoveMyTimeOff;
using Hospital.Application.Features.Doctors.Commands.RequestMyService;
using Hospital.Application.Features.Doctors.Commands.UpdateDoctor;
using Hospital.Application.Features.Doctors.Commands.UpdateMyAvailability;
using Hospital.Application.Features.Doctors.Commands.UpdateMyDoctorProfile;
using Hospital.Application.Features.Doctors.Queries.GetDoctor;
using Hospital.Application.Features.Doctors.Queries.GetMyDoctorProfile;
using Hospital.Application.Features.Doctors.Queries.ListDoctors;
using Hospital.Application.Features.Doctors.Queries.ListDoctorServiceRequests;
using Hospital.Application.Features.Doctors.Queries.ListMyAvailability;
using Hospital.Application.Features.Doctors.Queries.ListMyServices;
using Hospital.Application.Features.Doctors.Queries.ListMyTimeOff;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Doctors;

public sealed record CreateDoctorRequest(
    Guid DepartmentId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Specialty,
    string DiplomaNo,
    string? Phone);

public sealed record UpdateDoctorRequest(
    Guid DepartmentId,
    string FirstName,
    string LastName,
    string Specialty,
    string DiplomaNo,
    string? Phone);

public sealed record UpdateMyDoctorProfileRequest(string Specialty, string? Phone);

public sealed record AssignDoctorServicesRequest(IReadOnlyList<Guid> ServiceIds);

public sealed record RequestServiceRequest(Guid ServiceId);

public sealed record AvailabilityRequest(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record AddTimeOffRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string? Reason);

internal sealed class DoctorEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        MapSelfService(app.MapGroup("/api/doctors/me").WithTags("Doctors (self)").RequireAuthorization(Policies.Doctor));
        MapAdmin(app.MapGroup("/api/doctors").WithTags("Doctors (admin)").RequireAuthorization(Policies.Admin));

        app.MapGroup("/api/doctor-service-requests")
            .WithTags("Doctors (admin)")
            .RequireAuthorization(Policies.Admin)
            .MapGet("/", async (string? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListDoctorServiceRequestsQuery(status, page, pageSize), ct)).MatchPaged())
            .Produces<IReadOnlyList<DoctorServiceRequestDto>>(StatusCodes.Status200OK);
    }

    /// <summary>Doktorun kendi profili, uygunluğu, izinleri ve hizmet talepleri. Kimlik her zaman belirteçten gelir.</summary>
    private static void MapSelfService(RouteGroupBuilder group)
    {
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetMyDoctorProfileQuery(), ct)).Match(Results.Ok))
            .Produces<DoctorDetailDto>(StatusCodes.Status200OK);

        group.MapPut("/", async (UpdateMyDoctorProfileRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(new UpdateMyDoctorProfileCommand(request.Specialty, request.Phone), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        // Hizmet talebi: yönetici onaylayana kadar randevu alınamaz.
        group.MapGet("/services", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListMyServicesQuery(), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<DoctorServiceDto>>(StatusCodes.Status200OK);

        group.MapPost("/services", async (RequestServiceRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RequestMyServiceCommand(request.ServiceId), ct);
            return result.IsSuccess ? Results.Accepted() : result.ToProblem();
        })
            .Produces(StatusCodes.Status202Accepted);

        group.MapDelete("/services/{serviceId:guid}", async (Guid serviceId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RemoveMyServiceCommand(serviceId), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/availability", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListMyAvailabilityQuery(), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<DoctorAvailabilityDto>>(StatusCodes.Status200OK);

        group.MapPost("/availability", async (AvailabilityRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new AddMyAvailabilityCommand(request.Day, request.StartTime, request.EndTime, request.ValidFrom, request.ValidTo), ct);

            return result.Match(id => Results.Created($"/api/doctors/me/availability/{id}", new CreatedResponse(id)));
        })
            .Produces<CreatedResponse>(StatusCodes.Status201Created);

        group.MapPut("/availability/{id:guid}", async (Guid id, AvailabilityRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(
                new UpdateMyAvailabilityCommand(id, request.Day, request.StartTime, request.EndTime, request.ValidFrom, request.ValidTo), ct))
            .ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/availability/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RemoveMyAvailabilityCommand(id), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/time-off", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListMyTimeOffQuery(), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<DoctorTimeOffDto>>(StatusCodes.Status200OK);

        group.MapPost("/time-off", async (AddTimeOffRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddMyTimeOffCommand(request.StartAt, request.EndAt, request.Reason), ct);
            return result.Match(id => Results.Created($"/api/doctors/me/time-off/{id}", new CreatedResponse(id)));
        })
            .Produces<CreatedResponse>(StatusCodes.Status201Created);

        group.MapDelete("/time-off/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RemoveMyTimeOffCommand(id), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>Yönetici işlemleri: kapsam (şube) denetimi handler'larda uygulanır.</summary>
    private static void MapAdmin(RouteGroupBuilder group)
    {
        group.MapGet("/", async (Guid? hospitalId, Guid? departmentId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListDoctorsQuery(hospitalId, departmentId, page, pageSize), ct)).MatchPaged())
            .Produces<IReadOnlyList<DoctorDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateDoctorRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateDoctorCommand(
                    request.DepartmentId,
                    request.Email,
                    request.Password,
                    request.FirstName,
                    request.LastName,
                    request.Specialty,
                    request.DiplomaNo,
                    request.Phone),
                ct);

            return result.Match(id => Results.Created($"/api/doctors/{id}", new CreatedResponse(id)));
        })
            .Produces<CreatedResponse>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetDoctorQuery(id), ct)).Match(Results.Ok))
            .Produces<DoctorDetailDto>(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}", async (Guid id, UpdateDoctorRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(
                new UpdateDoctorCommand(
                    id, request.DepartmentId, request.FirstName, request.LastName, request.Specialty, request.DiplomaNo, request.Phone),
                ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        // Doğrudan atama: onaylı hizmet kümesini verilen listeyle eşitler (onay adımı yok).
        group.MapPut("/{id:guid}/services", async (Guid id, AssignDoctorServicesRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AssignDoctorServicesCommand(id, request.ServiceIds), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{doctorId:guid}/services/{serviceId:guid}/approve",
            async (Guid doctorId, Guid serviceId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ApproveDoctorServiceCommand(doctorId, serviceId), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{doctorId:guid}/services/{serviceId:guid}/reject",
            async (Guid doctorId, Guid serviceId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RejectDoctorServiceCommand(doctorId, serviceId), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{doctorId:guid}/services/{serviceId:guid}",
            async (Guid doctorId, Guid serviceId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoveDoctorServiceCommand(doctorId, serviceId), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);
    }
}
