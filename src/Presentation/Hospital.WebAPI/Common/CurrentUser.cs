using System.Security.Claims;
using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;

namespace Hospital.WebAPI.Common;

/// <summary>Cookie biletindeki claim'lerden istek sahibini okur.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuid(Principal?.FindFirstValue(ClaimTypes.NameIdentifier));

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public Guid? OrganizationId => ParseGuid(Principal?.FindFirstValue(ScopeClaimTypes.OrganizationId));

    public Guid? HospitalId => ParseGuid(Principal?.FindFirstValue(ScopeClaimTypes.HospitalId));

    public string? AccessLevel => Principal?.FindFirstValue(ScopeClaimTypes.AccessLevel);

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
