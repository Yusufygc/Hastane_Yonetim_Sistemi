using Hospital.Application.Security;

namespace Hospital.Application.Features.Admins;

public interface IAdminReader
{
    /// <summary>Süper yönetici organizasyonun tüm yöneticilerini, şube yöneticisi yalnız kendi şubesinin yöneticilerini görür.</summary>
    Task<IReadOnlyList<AdminDto>> ListAsync(AdminScope scope, CancellationToken cancellationToken);
}

/// <param name="AccessLevel">SUPER_ADMIN veya BRANCH_MANAGER.</param>
public sealed record AdminDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string AccessLevel,
    Guid OrganizationId,
    Guid? HospitalId,
    bool IsActive);

public static class AdminErrors
{
    public static readonly Wrappers.Error SuperAdminRequired =
        Wrappers.Error.Forbidden("admin.super_admin_required", "Bu işlem yalnızca süper yönetici tarafından yapılabilir.");
}
