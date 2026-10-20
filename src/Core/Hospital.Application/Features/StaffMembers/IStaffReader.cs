using Hospital.Application.Security;

namespace Hospital.Application.Features.StaffMembers;

public interface IStaffReader
{
    /// <summary>Kapsam içindeki personel; <paramref name="hospitalId"/> verilirse yalnız o şube.</summary>
    Task<IReadOnlyList<StaffDto>> ListAsync(AdminScope scope, Guid? hospitalId, CancellationToken cancellationToken);
}

public sealed record StaffDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Title,
    Guid HospitalId,
    Guid? DepartmentId,
    bool IsActive);

public static class StaffErrors
{
    public static readonly Wrappers.Error DepartmentInvalid =
        Wrappers.Error.Validation("staff.department_invalid", "Bölüm bu şubeye ait ve aktif olmalıdır.");
}
