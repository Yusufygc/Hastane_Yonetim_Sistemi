using System.Net;
using AwesomeAssertions;

namespace Hospital.IntegrationTests;

public class HealthEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Health_veritabani_ayaktayken_200_doner()
    {
        // Program.cs yapılandırmayı erken okuduğu için ortam değişkeni en güvenilir yoldur.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", postgres.ConnectionString);

        try
        {
            await using var factory = new HospitalApiFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        }
    }
}
