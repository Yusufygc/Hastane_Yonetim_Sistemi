using Hospital.Application.Extensions;
using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;
using Hospital.Persistence.Seeding;
using Hospital.Persistence.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Persistence.Extensions;

/// <summary>Persistence katmanının DI kaydı. Program.cs'te <c>builder.Services.AddPersistence()</c> ile çağrılır.</summary>
public static class PersistenceExt
{
    private const string FeaturesNamespace = "Hospital.Persistence.Features";

    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Bağlantı dizesi çalışma anında çözülür (testler yapılandırmayı geçersiz kılabilsin diye).
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default ayarı bulunamadı.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        // Repository, EF konfigürasyonu ve okuma servisleri Features/** altından otomatik kaydedilir.
        return services.AddFeatureImplementations(AssemblyReference.Assembly, FeaturesNamespace);
    }
}
