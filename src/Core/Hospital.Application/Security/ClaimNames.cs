namespace Hospital.Application.Security;

/// <summary>
/// JWT erişim belirtecindeki claim adları (kısa, standart adlar; <c>MapInboundClaims</c> kapalıdır).
/// Kapsam claim'leri (organization_id, hospital_id, access_level) Domain'deki <c>ScopeClaimTypes</c> ile aynıdır.
/// </summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string SecurityStamp = "security_stamp";
    public const string TokenId = "jti";
}
