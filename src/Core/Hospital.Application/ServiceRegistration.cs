using System.Reflection;
using FluentValidation;
using Hospital.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Application;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handler'lar Mediator source generator ile derleme zamanında bulunur (Features/** altı).
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;

            // Sıra: loglama (dış) → doğrulama (iç) → handler.
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
        });

        // Validator'lar da feature klasörlerinden taranarak kaydedilir.
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        services.AddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// <paramref name="assembly"/> içindeki <paramref name="featuresNamespace"/> altındaki somut sınıfları,
    /// uyguladıkları Hospital.Application arayüzleriyle otomatik kaydeder (ör. UserRepository → IUserRepository).
    /// Persistence ve Infrastructure katmanları bunu kullanır; yeni feature kayıt dosyasını değiştirmez.
    /// </summary>
    public static IServiceCollection AddFeatureImplementations(
        this IServiceCollection services,
        Assembly assembly,
        string featuresNamespace)
    {
        var implementations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && type.Namespace is not null
                           && type.Namespace.StartsWith(featuresNamespace, StringComparison.Ordinal));

        foreach (var implementation in implementations)
        {
            var contracts = implementation.GetInterfaces()
                .Where(i => i.Namespace is not null
                            && i.Namespace.StartsWith("Hospital.Application", StringComparison.Ordinal));

            foreach (var contract in contracts)
            {
                services.AddScoped(contract, implementation);
            }
        }

        return services;
    }
}
