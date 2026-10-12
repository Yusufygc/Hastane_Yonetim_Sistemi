using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

public class RateLimitTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Giris_ucu_limit_asilinca_429_doner()
    {
        var factory = fixture.CreateFactory(new Dictionary<string, string?> { ["RateLimiting:Auth:PermitLimit"] = "3" });
        var client = new ApiClient(factory.CreateClient());
        var account = TestAccount.New();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsync("/api/auth/login", account.LoginBody());
            statuses.Add(response.StatusCode);
        }

        statuses.Take(3).Should().AllSatisfy(status => status.Should().Be(HttpStatusCode.Unauthorized));
        statuses.Skip(3).Should().AllSatisfy(status => status.Should().Be(HttpStatusCode.TooManyRequests));
    }
}
