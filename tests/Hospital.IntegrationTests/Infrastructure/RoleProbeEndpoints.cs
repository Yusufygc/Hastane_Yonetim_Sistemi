using Hospital.WebAPI.Common;
using Hospital.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>
/// Yalnızca testlerde kayıt edilen, rol politikalarını doğrulamak için kullanılan uçlar
/// (ör. hasta doktor ucuna giremez). Gerçek uygulamada bulunmaz.
/// </summary>
internal sealed class RoleProbeEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/test/patient-only", () => "ok").RequireAuthorization(Policies.Patient);
        app.MapGet("/api/test/doctor-only", () => "ok").RequireAuthorization(Policies.Doctor);
        app.MapGet("/api/test/admin-only", () => "ok").RequireAuthorization(Policies.Admin);
        app.MapGet("/api/test/super-admin-only", () => "ok").RequireAuthorization(Policies.SuperAdmin);
    }
}
