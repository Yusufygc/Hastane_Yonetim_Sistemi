using Hospital.Application.Features.Services;
using Hospital.Application.Features.Services.Commands.CreateService;
using Hospital.Application.Features.Services.Commands.DeactivateService;
using Hospital.Application.Features.Services.Commands.UpdateService;
using Hospital.Application.Features.Services.Queries.GetService;
using Hospital.Application.Features.Services.Queries.ListServices;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Services;

public sealed record CreateServiceRequest(
    Guid HospitalId,
    Guid? DepartmentId,
    string Name,
    int DurationMinutes,
    decimal Price,
    string Currency);

public sealed record UpdateServiceRequest(
    Guid? DepartmentId,
    string Name,
    int DurationMinutes,
    decimal Price,
    string Currency);

internal sealed class ServiceEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/services")
            .WithTags("Services")
            .RequireAuthorization(Policies.Admin);

        group.MapGet("/", async (Guid? hospitalId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListServicesQuery(hospitalId), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<ServiceDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateServiceRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateServiceCommand(
                    request.HospitalId,
                    request.DepartmentId,
                    request.Name,
                    request.DurationMinutes,
                    request.Price,
                    request.Currency),
                ct);

            return result.Match(id => Results.Created($"/api/services/{id}", new CreatedResponse(id)));
        })
            .Produces<CreatedResponse>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetServiceQuery(id), ct)).Match(Results.Ok))
            .Produces<ServiceDto>(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}", async (Guid id, UpdateServiceRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(
                new UpdateServiceCommand(id, request.DepartmentId, request.Name, request.DurationMinutes, request.Price, request.Currency),
                ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new DeactivateServiceCommand(id), ct)).ToNoContent())
            .Produces(StatusCodes.Status204NoContent);
    }
}
