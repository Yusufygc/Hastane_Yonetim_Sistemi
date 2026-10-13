using Hospital.Application.Security;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Hospitals;

/// <summary>Yazma tarafı: izlenen (tracked) aggregate'leri döner.</summary>
public interface IHospitalRepository
{
    Task<HospitalBranch?> GetByIdAsync(Guid id, bool includeWorkingHours, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludeId, CancellationToken cancellationToken);

    void Add(HospitalBranch hospital);
}

/// <summary>Okuma tarafı: yönetici kapsamına göre süzülmüş DTO projeksiyonları.</summary>
public interface IHospitalReader
{
    Task<IReadOnlyList<HospitalDto>> ListAsync(AdminScope scope, CancellationToken cancellationToken);

    Task<HospitalDetailDto?> GetDetailAsync(Guid id, AdminScope scope, CancellationToken cancellationToken);
}
