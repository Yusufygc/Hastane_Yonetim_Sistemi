using Hospital.Application.Features.Departments.Commands.CreateDepartment;
using Hospital.Application.Features.Departments.Commands.DeactivateDepartment;
using Hospital.Application.Features.Departments.Commands.UpdateDepartment;
using Hospital.Application.Features.Departments.Queries.ListDepartments;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Departments;

public sealed record DepartmentRequest(string Name);

internal sealed class DepartmentEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/hospitals/{hospitalId:guid}/departments", async (Guid hospitalId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListDepartmentsQuery(hospitalId), ct)).Match(Results.Ok))
            .WithTags("Departments")
            .RequireAuthorization(Policies.Admin);

        app.MapPost("/api/hospitals/{hospitalId:guid}/departments", async (Guid hospitalId, DepartmentRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateDepartmentCommand(hospitalId, request.Name), ct);
                return result.Match(id => Results.Created($"/api/departments/{id}", new { id }));
            })
            .WithTags("Departments")
            .RequireAuthorization(Policies.Admin);

        app.MapPut("/api/departments/{id:guid}", async (Guid id, DepartmentRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateDepartmentCommand(id, request.Name), ct)).ToNoContent())
            .WithTags("Departments")
            .RequireAuthorization(Policies.Admin);

        app.MapPost("/api/departments/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeactivateDepartmentCommand(id), ct)).ToNoContent())
            .WithTags("Departments")
            .RequireAuthorization(Policies.Admin);
    }
}
