using Hospital.Application;
using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;
using Hospital.Persistence.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Persistence;

public static class ServiceRegistration
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

        // Repository, EF konfigürasyonu ve okuma servisleri Features/** altından otomatik kaydedilir.
        return services.AddFeatureImplementations(AssemblyReference.Assembly, FeaturesNamespace);
    }
}
