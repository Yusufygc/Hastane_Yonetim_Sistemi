using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// Her endpoint ya AllowAnonymous olmalı ya da açık bir yetki politikası taşımalı.
/// Fallback policy tek başına "doktor ucuna hasta girer" hatasını yakalamaz; bu test yakalar.
/// </summary>
public class EndpointAuthorizationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public void Her_endpoint_acik_politika_veya_AllowAnonymous_tasimali()
    {
        var dataSource = fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        var endpoints = dataSource.Endpoints.OfType<RouteEndpoint>().ToList();
        endpoints.Should().NotBeEmpty();

        var offenders = endpoints
            .Where(endpoint => !IsExplicitlySecured(endpoint))
            .Select(endpoint => $"{endpoint.RoutePattern.RawText} ({endpoint.DisplayName})")
            .ToList();

        offenders.Should().BeEmpty("her endpoint AllowAnonymous veya açık bir politika (RequireAuthorization(Policies.X)) taşımalı");
    }

    private static bool IsExplicitlySecured(RouteEndpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return true;
        }

        return endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(data => !string.IsNullOrWhiteSpace(data.Policy) || !string.IsNullOrWhiteSpace(data.Roles));
    }
}
