using Hospital.Application.Features.Notifications;
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
    bool includeProbeEndpoints = false,
    bool useFakeEmailSender = true) : WebApplicationFactory<Program>
{
    /// <summary>Test göndericisi (yalnız <c>useFakeEmailSender</c> açıkken kayıtlıdır).</summary>
    public FakeEmailSender EmailSender => Services.GetRequiredService<FakeEmailSender>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var merged = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Database:MigrateOnStartup"] = "true",

            // JWT imzalama anahtarı (yalnız test değeri); Issuer/Audience appsettings.json varsayılanlarıdır.
            ["Jwt:SigningKey"] = TestJwt.SigningKey,

            // Hassas veri (T.C. kimlik no) şifreleme anahtarı (yalnız test değeri; Base64, 32 bayt).
            ["Security:DataProtection:Key"] = TestDataProtection.Key,

            // Varsayılan kurum/şube + örnek bölüm/hizmet + ilk süper yönetici (yalnız test değerleri).
            ["Database:SeedOnStartup"] = "true",
            ["Seed:SampleData"] = "true",
            ["Seed:SuperAdmin:Email"] = TestAdmin.Email,
            ["Seed:SuperAdmin:Password"] = TestAdmin.Password,

            // Örnek doktor (Dahiliye, Genel Muayene onaylı, hafta içi 09-12 uygun); yalnız test değerleri.
            ["Seed:SampleDoctor:Email"] = TestDoctor.SeededEmail,
            ["Seed:SampleDoctor:Password"] = TestDoctor.Password,

            // Testler aynı IP'den çok sayıda kayıt/giriş yapar; hız sınırı ayrı bir testte denenir.
            ["RateLimiting:Auth:PermitLimit"] = "1000",

            // E-posta outbox işleyicisi varsayılan olarak kapalı: testler outbox satırlarını ve IEmailOutboxStore'u
            // doğrudan, deterministik biçimde sınar. Arka plan işleyiciyi sınayan testler açıkça etkinleştirir.
            ["Email:Outbox:Enabled"] = "false",

            // Bekleyen randevu zaman aşımı işleyicisi de varsayılan kapalı: testler PendingAppointmentExpirer'ı doğrudan çağırır.
            ["Appointment:PendingExpiry:Enabled"] = "false",
        };

        if (settings is not null)
        {
            foreach (var (key, value) in settings)
            {
                merged[key] = value;
            }
        }

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(merged));

        if (useFakeEmailSender)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<FakeEmailSender>();
                services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<FakeEmailSender>());
            });
        }

        if (includeProbeEndpoints)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddTransient<IEndpoint, RoleProbeEndpoints>();
                services.AddTransient<IEndpoint, ErrorProbeEndpoints>();
            });
        }
    }
}
