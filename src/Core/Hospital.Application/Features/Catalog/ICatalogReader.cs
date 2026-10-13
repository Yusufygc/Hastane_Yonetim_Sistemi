using Hospital.Application.Features.Hospitals;

namespace Hospital.Application.Features.Catalog;

/// <summary>
/// Herkese açık (anonim) katalog okuyucusu. Yalnızca aktif kayıtları ve kişisel olmayan alanları döner.
/// Şube yoksa veya pasifse ilgili listeler <c>null</c> döner (varlık ayrımı yapılmaz).
/// </summary>
public interface ICatalogReader
{
    Task<IReadOnlyList<CatalogHospitalDto>> GetHospitalsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogDepartmentDto>?> GetDepartmentsAsync(Guid hospitalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogServiceDto>?> GetServicesAsync(Guid hospitalId, CancellationToken cancellationToken);
}

public sealed record CatalogHospitalDto(
    Guid Id,
    string Name,
    string? Address,
    string? Phone,
    string TimeZoneId,
    IReadOnlyList<WorkingHoursDto> WorkingHours);

public sealed record CatalogDepartmentDto(Guid Id, string Name);

public sealed record CatalogServiceDto(
    Guid Id,
    Guid? DepartmentId,
    string Name,
    int DurationMinutes,
    decimal Price,
    string Currency);

public static class CatalogErrors
{
    public static readonly Wrappers.Error HospitalNotFound =
        Wrappers.Error.NotFound("catalog.hospital_not_found", "Şube bulunamadı.");
}
