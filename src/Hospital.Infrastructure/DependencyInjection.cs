using Hospital.Application.Common.Abstractions;
using Hospital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Infrastructure;

public static class DependencyInjection
{
    private const string FeaturesNamespace = "Hospital.Infrastructure.Features";
    private const string ApplicationNamespace = "Hospital.Application";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Bağlantı dizesi çalışma anında çözülür (testler yapılandırmayı geçersiz kılabilsin diye).
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default ayarı bulunamadı.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        AddFeatureServices(services);

        return services;
    }

    /// <summary>
    /// Infrastructure/Features/** altındaki somut sınıfları, uyguladıkları Application arayüzleriyle
    /// otomatik kaydeder (ör. UserRepository → IUserRepository). Yeni feature bu dosyayı değiştirmez.
    /// </summary>
    private static void AddFeatureServices(IServiceCollection services)
    {
        var implementations = AssemblyReference.Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && type.Namespace is not null
                           && type.Namespace.StartsWith(FeaturesNamespace, StringComparison.Ordinal));

        foreach (var implementation in implementations)
        {
            var contracts = implementation.GetInterfaces()
                .Where(i => i.Namespace is not null && i.Namespace.StartsWith(ApplicationNamespace, StringComparison.Ordinal));

            foreach (var contract in contracts)
            {
                services.AddScoped(contract, implementation);
            }
        }
    }
}
