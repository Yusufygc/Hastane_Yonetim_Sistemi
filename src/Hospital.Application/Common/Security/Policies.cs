namespace Hospital.Application.Common.Security;

/// <summary>
/// Yetkilendirme politikası adları. Her endpoint açık bir politika ya da AllowAnonymous taşımalıdır.
/// </summary>
public static class Policies
{
    public const string Authenticated = nameof(Authenticated);
    public const string Patient = nameof(Patient);
    public const string Doctor = nameof(Doctor);
    public const string Admin = nameof(Admin);
    public const string SuperAdmin = nameof(SuperAdmin);
}
