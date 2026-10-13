using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Departments;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(Guid hospitalId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> HasActiveServicesAsync(Guid departmentId, CancellationToken cancellationToken);

    void Add(Department department);
}

public interface IDepartmentReader
{
    /// <summary>Şubenin tüm bölümleri (yönetici pasifleri de görür).</summary>
    Task<IReadOnlyList<DepartmentDto>> ListAsync(Guid hospitalId, CancellationToken cancellationToken);
}

public sealed record DepartmentDto(Guid Id, Guid HospitalId, string Name, bool IsActive);

public static class DepartmentErrors
{
    public static readonly Wrappers.Error NotFound =
        Wrappers.Error.NotFound("department.not_found", "Bölüm bulunamadı.");

    public static readonly Wrappers.Error NameTaken =
        Wrappers.Error.Conflict("department.name_taken", "Bu şubede aynı isimde bir bölüm zaten var.");

    public static readonly Wrappers.Error HasActiveServices =
        Wrappers.Error.Conflict("department.has_active_services", "Aktif hizmeti olan bölüm pasifleştirilemez.");
}
