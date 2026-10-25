using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Hospital.IntegrationTests;

/// <summary>
/// Yetki envanteri (veritabanı gerektirmez): uygulamadaki her API ucu <see cref="EndpointMatrix"/> tablosunda, tabloda yazan
/// politikayla birlikte bulunmalı ve tersi. Politikası gevşetilen uç veya yetkilendirilmeden eklenen yeni uç burada yakalanır.
/// </summary>
public class EndpointInventoryTests(DbLessApiFixture fixture) : IClassFixture<DbLessApiFixture>
{
    private IReadOnlyList<EndpointRule> Actual => EndpointMatrix.Discover(fixture.Service<EndpointDataSource>());

    [Fact]
    public void Her_uc_matriste_tam_politikasiyla_yazili_matriste_olmayan_uc_yok()
    {
        var actual = Actual.Select(r => r.ToString()).Order().ToList();
        var expected = EndpointMatrix.Rules.Select(r => r.ToString()).Order().ToList();

        actual.Should().BeEquivalentTo(
            expected,
            options => options.WithStrictOrdering(),
            "uç noktalar ve politikaları EndpointMatrix'teki gözden geçirilmiş tabloyla birebir aynı olmalı. " +
            "Yeni uç ekledinizse veya politika değiştirdiyseniz tabloyu bilinçli olarak güncelleyin.");
    }

    [Fact]
    public void Matriste_yinelenen_kayit_yok()
    {
        EndpointMatrix.Rules.Select(r => $"{r.Method} {r.Route}").Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Yalniz_bilinen_politikalar_kullanilir_ve_hicbir_api_ucu_politikasiz_degildir()
    {
        var known = new[] { EndpointMatrix.Anonymous, "Authenticated", "Patient", "Doctor", "Admin", "SuperAdmin" };

        Actual.Should().OnlyContain(r => known.Contains(r.Policy), "her uç tek ve bilinen bir politika (veya AllowAnonymous) taşımalı");
    }

    [Fact]
    public void Anonim_uclar_yalniz_kimlik_girisi_ve_katalogdur()
    {
        var anonymous = Actual.Where(r => r.Policy == EndpointMatrix.Anonymous).Select(r => r.Route).ToList();

        anonymous.Should().OnlyContain(route => route.StartsWith("/api/auth/") || route.StartsWith("/api/catalog/"),
            "kişisel veya yönetim verisi sunan uç anonim olmamalı");
        anonymous.Where(r => r.StartsWith("/api/auth/")).Should().BeEquivalentTo("/api/auth/register", "/api/auth/login", "/api/auth/refresh");
    }

    [Fact]
    public void Yazma_uclari_anonim_degildir_yalniz_kimlik_uclari_haric()
    {
        Actual.Where(r => r.Method != "GET" && r.Policy == EndpointMatrix.Anonymous)
            .Select(r => r.Route)
            .Should().BeEquivalentTo("/api/auth/register", "/api/auth/login", "/api/auth/refresh");
    }

    [Fact]
    public void Yonetim_uclari_hasta_veya_doktor_politikasinda_olamaz()
    {
        // Yönetim kaynakları (kurum, şube, hizmet, personel, yönetici, kullanıcı) yalnız Admin/SuperAdmin politikalıdır.
        var adminPrefixes = new[] { "/api/organization", "/api/hospitals", "/api/services", "/api/staff", "/api/admins", "/api/users", "/api/departments" };

        Actual.Where(r => adminPrefixes.Any(p => r.Route.StartsWith(p, StringComparison.Ordinal)))
            .Should().OnlyContain(r => r.Policy == "Admin" || r.Policy == "SuperAdmin");
    }

    [Fact]
    public void Me_uclari_yalniz_ilgili_role_aittir()
    {
        Actual.Where(r => r.Route.StartsWith("/api/doctors/me")).Should().OnlyContain(r => r.Policy == "Doctor");
        Actual.Where(r => r.Route.StartsWith("/api/patients/me")).Should().OnlyContain(r => r.Policy == "Patient");
    }
}
