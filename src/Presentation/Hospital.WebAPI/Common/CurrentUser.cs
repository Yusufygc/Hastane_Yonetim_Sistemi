using System.Security.Claims;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Domain.Entities.Users;

namespace Hospital.WebAPI.Common;

/// <summary>JWT erişim belirtecindeki claim'lerden istek sahibini okurum (veritabanına gitmez).</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuid(Principal?.FindFirstValue(ClaimNames.Subject));

    public string? Email => Principal?.FindFirstValue(ClaimNames.Email);

    public string? Role => Principal?.FindFirstValue(ClaimNames.Role);

    public Guid? OrganizationId => ParseGuid(Principal?.FindFirstValue(ScopeClaimTypes.OrganizationId));

    public Guid? HospitalId => ParseGuid(Principal?.FindFirstValue(ScopeClaimTypes.HospitalId));

    public string? AccessLevel => Principal?.FindFirstValue(ScopeClaimTypes.AccessLevel);

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
