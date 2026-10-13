using Hospital.Application.Security;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Services;

public interface IServiceRepository
{
    Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(Guid hospitalId, string name, Guid? excludeId, CancellationToken cancellationToken);

    void Add(Service service);
}

public interface IServiceReader
{
    /// <summary>Kapsam içindeki hizmetler; <paramref name="hospitalId"/> verilirse yalnız o şube.</summary>
    Task<IReadOnlyList<ServiceDto>> ListAsync(AdminScope scope, Guid? hospitalId, CancellationToken cancellationToken);

    Task<ServiceDto?> GetAsync(Guid id, AdminScope scope, CancellationToken cancellationToken);
}

public sealed record ServiceDto(
    Guid Id,
    Guid HospitalId,
    Guid? DepartmentId,
    string Name,
    int DurationMinutes,
    decimal Price,
    string Currency,
    bool IsActive);

public static class ServiceErrors
{
    public static readonly Wrappers.Error NotFound =
        Wrappers.Error.NotFound("service.not_found", "Hizmet bulunamadı.");

    public static readonly Wrappers.Error NameTaken =
        Wrappers.Error.Conflict("service.name_taken", "Bu şubede aynı isimde bir hizmet zaten var.");

    public static readonly Wrappers.Error DepartmentInvalid =
        Wrappers.Error.Validation("service.department_invalid", "Bölüm bu şubeye ait ve aktif olmalıdır.");
}
