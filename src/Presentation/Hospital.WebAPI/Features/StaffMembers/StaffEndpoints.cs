using Hospital.Application.Features.StaffMembers;
using Hospital.Application.Features.StaffMembers.Commands.CreateStaff;
using Hospital.Application.Features.StaffMembers.Queries.ListStaff;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.StaffMembers;

public sealed record CreateStaffRequest(
    Guid HospitalId,
    Guid? DepartmentId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Title);

internal sealed class StaffEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .RequireAuthorization(Policies.Admin);

        group.MapGet("/", async (Guid? hospitalId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListStaffQuery(hospitalId, page, pageSize), ct)).MatchPaged())
            .Produces<IReadOnlyList<StaffDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateStaffRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateStaffCommand(
                    request.HospitalId,
                    request.DepartmentId,
                    request.Email,
                    request.Password,
                    request.FirstName,
                    request.LastName,
                    request.Title),
                ct);

            return result.Match(id => Results.Created($"/api/staff/{id}", new CreatedResponse(id)));
        })
            .Produces<CreatedResponse>(StatusCodes.Status201Created);
    }
}
