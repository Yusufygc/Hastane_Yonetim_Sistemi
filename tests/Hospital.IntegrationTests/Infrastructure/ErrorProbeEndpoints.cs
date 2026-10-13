using Hospital.Application.Security;
using Hospital.Domain.Common;
using Hospital.WebAPI.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hospital.IntegrationTests.Infrastructure;

public sealed record ProbeBody(Guid? Id, DateOnly Date, string Text, int Count);

/// <summary>Yalnızca testlerde kayıtlı: hata işleme davranışını (500, 422, gövde bağlama) doğrular.</summary>
internal sealed class ErrorProbeEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/test/error/unhandled", () => { throw new InvalidOperationException("Beklenmeyen test hatası."); })
            .RequireAuthorization(Policies.Authenticated);

        app.MapGet("/api/test/error/domain", () => { throw new DomainException("Kural ihlali testi."); })
            .RequireAuthorization(Policies.Authenticated);

        app.MapPost("/api/test/error/body", (ProbeBody body) => Results.Ok(body))
            .RequireAuthorization(Policies.Authenticated);

        app.MapGet("/api/test/error/query", (Guid id, int count) => Results.Ok(new { id, count }))
            .RequireAuthorization(Policies.Authenticated);
    }
}
