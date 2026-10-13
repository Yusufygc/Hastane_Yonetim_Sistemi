using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Security;

/// <summary>
/// Yöneticinin yönetebildiği kapsam: süper yönetici organizasyondaki tüm şubeleri, şube yöneticisi yalnızca
/// atandığı şubeyi yönetir. Kapsam cookie claim'lerinden okunur; handler'lar her işlemde bunu uygular.
/// </summary>
public sealed record AdminScope(Guid OrganizationId, Guid? HospitalId)
{
    public bool IsSuperAdmin => HospitalId is null;

    public bool CanAccessHospital(Guid hospitalOrganizationId, Guid hospitalId) =>
        hospitalOrganizationId == OrganizationId && (HospitalId is null || HospitalId == hospitalId);

    /// <summary>Kullanıcı yönetici değilse veya kapsam claim'leri eksik/tutarsızsa <c>null</c> döner.</summary>
    public static AdminScope? From(ICurrentUser user)
    {
        if (user.Role != RoleNames.Admin || user.OrganizationId is not { } organizationId)
        {
            return null;
        }

        return user.AccessLevel switch
        {
            AdminAccessLevelNames.SuperAdmin => new AdminScope(organizationId, null),
            AdminAccessLevelNames.BranchManager when user.HospitalId is { } hospitalId => new AdminScope(organizationId, hospitalId),
            _ => null,
        };
    }
}
