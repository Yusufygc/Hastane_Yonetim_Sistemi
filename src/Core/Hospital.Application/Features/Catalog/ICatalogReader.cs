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

    /// <summary>
    /// Şubenin aktif doktorları; yalnızca en az bir <b>onaylı</b> hizmeti olanlar. <paramref name="serviceId"/> verilirse
    /// yalnızca o hizmeti onaylı sunanlar. Şube yoksa/pasifse <c>null</c>.
    /// </summary>
    Task<IReadOnlyList<CatalogDoctorDto>?> GetDoctorsAsync(Guid hospitalId, Guid? serviceId, CancellationToken cancellationToken);
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

/// <summary>Kişisel olmayan doktor bilgisi: e-posta, telefon, diploma no yer almaz.</summary>
public sealed record CatalogDoctorDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Specialty,
    Guid DepartmentId,
    string DepartmentName,
    IReadOnlyList<CatalogDoctorServiceDto> Services);

public sealed record CatalogDoctorServiceDto(Guid Id, string Name, int DurationMinutes, decimal Price, string Currency);

public static class CatalogErrors
{
    public static readonly Wrappers.Error HospitalNotFound =
        Wrappers.Error.NotFound("catalog.hospital_not_found", "Şube bulunamadı.");
}
