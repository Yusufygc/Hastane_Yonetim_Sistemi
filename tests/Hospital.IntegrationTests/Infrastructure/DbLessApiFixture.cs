using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>
/// Veritabanı gerektirmeyen uygulama örneği: migration/seed kapalı, yalnız uç nokta tablosu ve OpenAPI belgesi okunur.
/// Sözleşme/envanter testleri PostgreSQL olmadan (yerelde de) çalışır.
/// </summary>
public sealed class DbLessApiFixture : IDisposable
{
    public DbLessApiFixture()
    {
        Factory = new HospitalApiFactory(
            "Host=localhost;Port=5432;Database=none;Username=none;Password=none",
            new Dictionary<string, string?>
            {
                ["Database:MigrateOnStartup"] = "false",
                ["Database:SeedOnStartup"] = "false",
                ["OpenApi:Enabled"] = "true",
                ["Frontend:BaseUrl"] = "http://localhost:5173",
            });
    }

    public HospitalApiFactory Factory { get; }

    public T Service<T>() where T : notnull => Factory.Services.GetRequiredService<T>();

    public void Dispose() => Factory.Dispose();
}
