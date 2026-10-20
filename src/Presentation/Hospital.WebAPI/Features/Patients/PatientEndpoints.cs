using Hospital.Application.Features.Patients.Commands.UpdateMyPatientProfile;
using Hospital.Application.Features.Patients.Queries.GetMyPatientProfile;
using Hospital.Application.Security;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Patients;

public sealed record UpdatePatientProfileRequest(string FirstName, string LastName, string? Phone, string? BloodType);

internal sealed class PatientEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients/me")
            .WithTags("Patients")
            .RequireAuthorization(Policies.Patient);

        // T.C. kimlik no yanıtta maskelidir (ilk 3 + son 2 hane).
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetMyPatientProfileQuery(), ct)).Match(Results.Ok));

        group.MapPut("/", async (UpdatePatientProfileRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(
                new UpdateMyPatientProfileCommand(request.FirstName, request.LastName, request.Phone, request.BloodType), ct))
            .Match(Results.Ok));
    }
}
