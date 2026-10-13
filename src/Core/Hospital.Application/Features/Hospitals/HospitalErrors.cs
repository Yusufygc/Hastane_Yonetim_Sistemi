using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Hospitals;

public static class HospitalErrors
{
    public static readonly Error Forbidden =
        Error.Forbidden("facility.forbidden", "Bu işlem için yetkiniz yok.");

    public static readonly Error NotFound =
        Error.NotFound("hospital.not_found", "Şube bulunamadı.");

    public static readonly Error NameTaken =
        Error.Conflict("hospital.name_taken", "Bu isimde bir şube zaten var.");

    /// <summary>
    /// Yönetici kapsamı içinde bir şubeye erişim denetimi. Kapsam dışı şube "bulunamadı" gibi davranır
    /// (varlığı sızdırılmaz). Erişim varsa <c>null</c> döner.
    /// </summary>
    public static Error? CheckAccess(AdminScope? scope, HospitalBranch? hospital)
    {
        if (scope is null)
        {
            return Forbidden;
        }

        return hospital is null || !scope.CanAccessHospital(hospital.OrganizationId, hospital.Id)
            ? NotFound
            : null;
    }
}
