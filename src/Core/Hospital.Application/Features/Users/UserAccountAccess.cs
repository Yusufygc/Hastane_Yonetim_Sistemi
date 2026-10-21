using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Users;

public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("user.not_found", "Kullanıcı bulunamadı.");

    public static readonly Error HasFutureAppointments =
        Error.Conflict("user.has_future_appointments", "Kullanıcının gelecekte aktif (bekleyen/onaylı) randevusu var; önce randevuları iptal edin.");

    public static readonly Error CannotModifySelf =
        Error.Conflict("user.cannot_modify_self", "Kendi hesabınızı pasifleştiremezsiniz.");
}

/// <summary>Yöneticinin bir kullanıcı hesabı üzerinde işlem yapıp yapamayacağını türüne göre belirler.</summary>
internal static class UserAccountAccess
{
    /// <summary>
    /// Doktor/personel: bağlı olduğu şube yönetici kapsamında olmalı. Yönetici ve hasta hesapları: yalnızca süper yönetici
    /// (yönetici için aynı organizasyon). Kapsam dışı hesap "bulunamadı" gibi davranır (varlık sızmaz).
    /// </summary>
    public static async Task<Result<User>> LoadForAdminAsync(
        Guid userId,
        ICurrentUser currentUser,
        IUserRepository users,
        IDepartmentRepository departments,
        IHospitalRepository hospitals,
        CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var inScope = user switch
        {
            Doctor doctor => await HospitalInScopeAsync(scope, doctor.DepartmentId, null, departments, hospitals, cancellationToken),
            Staff staff => await HospitalInScopeAsync(scope, null, staff.HospitalId, departments, hospitals, cancellationToken),
            Admin admin => scope.IsSuperAdmin && admin.OrganizationId == scope.OrganizationId,
            _ => scope.IsSuperAdmin, // hasta: kuruma bağlı değil
        };

        return inScope ? user : UserErrors.NotFound;
    }

    private static async Task<bool> HospitalInScopeAsync(
        AdminScope scope,
        Guid? departmentId,
        Guid? hospitalId,
        IDepartmentRepository departments,
        IHospitalRepository hospitals,
        CancellationToken cancellationToken)
    {
        if (departmentId is { } id)
        {
            hospitalId = (await departments.GetByIdAsync(id, cancellationToken))?.HospitalId;
        }

        if (hospitalId is not { } resolved)
        {
            return false;
        }

        var hospital = await hospitals.GetByIdAsync(resolved, includeWorkingHours: false, cancellationToken);
        return hospital is not null && scope.CanAccessHospital(hospital.OrganizationId, hospital.Id);
    }
}
