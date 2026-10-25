using Hospital.Application.Features.Catalog;
using Hospital.Application.Features.Catalog.Queries.GetCatalogDepartments;
using Hospital.Application.Features.Catalog.Queries.GetCatalogDoctors;
using Hospital.Application.Features.Catalog.Queries.GetCatalogHospitals;
using Hospital.Application.Features.Catalog.Queries.GetCatalogServices;
using Hospital.WebAPI.Common;
using Mediator;

namespace Hospital.WebAPI.Features.Catalog;

/// <summary>Herkese açık (anonim) katalog: yalnızca aktif kayıtlar, kişisel veri yok. Randevu almak için giriş gerekir.</summary>
internal sealed class CatalogEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog")
            .WithTags("Catalog")
            .AllowAnonymous();

        group.MapGet("/hospitals", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCatalogHospitalsQuery(), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<CatalogHospitalDto>>(StatusCodes.Status200OK);

        group.MapGet("/hospitals/{id:guid}/departments", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCatalogDepartmentsQuery(id), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<CatalogDepartmentDto>>(StatusCodes.Status200OK);

        group.MapGet("/hospitals/{id:guid}/services", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCatalogServicesQuery(id), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<CatalogServiceDto>>(StatusCodes.Status200OK);

        // Yalnızca en az bir onaylı hizmeti olan aktif doktorlar; serviceId verilirse o hizmeti sunanlar.
        group.MapGet("/doctors", async (Guid hospitalId, Guid? serviceId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCatalogDoctorsQuery(hospitalId, serviceId), ct)).Match(Results.Ok))
            .Produces<IReadOnlyList<CatalogDoctorDto>>(StatusCodes.Status200OK);
    }
}
