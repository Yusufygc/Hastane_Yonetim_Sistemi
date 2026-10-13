using Hospital.WebAPI.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Uygulamayı belirtilen (geçici) veritabanıyla ayağa kaldırır; ayarlar ve servisler geçersiz kılınabilir.</summary>
public sealed class HospitalApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    bool includeProbeEndpoints = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var merged = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Database:MigrateOnStartup"] = "true",

            // Varsayılan kurum/şube + örnek bölüm/hizmet + ilk süper yönetici (yalnız test değerleri).
            ["Database:SeedOnStartup"] = "true",
            ["Seed:SampleData"] = "true",
            ["Seed:SuperAdmin:Email"] = TestAdmin.Email,
            ["Seed:SuperAdmin:Password"] = TestAdmin.Password,

            // Testler aynı IP'den çok sayıda kayıt/giriş yapar; hız sınırı ayrı bir testte denenir.
            ["RateLimiting:Auth:PermitLimit"] = "1000",
        };

        if (settings is not null)
        {
            foreach (var (key, value) in settings)
            {
                merged[key] = value;
            }
        }

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(merged));

        if (includeProbeEndpoints)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddTransient<IEndpoint, RoleProbeEndpoints>();
                services.AddTransient<IEndpoint, AppointmentProbeEndpoints>();
            });
        }
    }
}
