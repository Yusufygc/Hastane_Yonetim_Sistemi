using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>Rol politikaları: hasta doktor/yönetici uçlarına giremez (403), giriş yapmayan 401 alır.</summary>
public class RoleAuthorizationTests : IClassFixture<ApiFixture>
{
    private readonly HospitalApiFactory _factory;

    public RoleAuthorizationTests(ApiFixture fixture)
    {
        _factory = fixture.CreateFactory(includeRoleProbeEndpoints: true);
    }

    [Theory]
    [InlineData("/api/test/doctor-only")]
    [InlineData("/api/test/admin-only")]
    [InlineData("/api/test/super-admin-only")]
    public async Task Hasta_doktor_ve_yonetici_uclarina_giremez_403(string url)
    {
        var client = new ApiClient(_factory.CreateClient());
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hasta_kendi_ucuna_girebilir_200()
    {
        var client = new ApiClient(_factory.CreateClient());
        await client.RegisterAndLoginAsync();

        var response = await client.GetAsync("/api/test/patient-only");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/test/patient-only")]
    [InlineData("/api/test/doctor-only")]
    [InlineData("/api/test/admin-only")]
    public async Task Giris_yapmayan_korumali_uclarda_401_alir(string url)
    {
        var client = new ApiClient(_factory.CreateClient());

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
