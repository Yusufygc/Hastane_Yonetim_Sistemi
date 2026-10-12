namespace Hospital.Domain.Entities.Users;

public enum UserRole
{
    Patient = 1,
    Doctor = 2,
    Staff = 3,
    Admin = 4,
}

/// <summary>Cookie bileti ve politikalarda kullanılan rol adları (veritabanındaki değerlerle aynı).</summary>
public static class RoleNames
{
    public const string Patient = "PATIENT";
    public const string Doctor = "DOCTOR";
    public const string Staff = "STAFF";
    public const string Admin = "ADMIN";

    public static string From(UserRole role) => role.ToString().ToUpperInvariant();
}

/// <summary>Kullanıcının kapsamını (şube/organizasyon) taşıyan claim adları.</summary>
public static class ScopeClaimTypes
{
    public const string OrganizationId = "organization_id";
    public const string HospitalId = "hospital_id";
    public const string AccessLevel = "access_level";
}
