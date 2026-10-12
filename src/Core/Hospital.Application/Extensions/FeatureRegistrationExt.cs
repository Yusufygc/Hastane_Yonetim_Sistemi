using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Application.Extensions;

public static class FeatureRegistrationExt
{
    /// <summary>
    /// <paramref name="assembly"/> içindeki <paramref name="featuresNamespace"/> altındaki somut sınıfları,
    /// uyguladıkları Hospital.Application arayüzleriyle otomatik kaydeder (ör. UserRepository → IUserRepository).
    /// Persistence ve Infrastructure katmanları kendi Ext sınıflarında bunu kullanır; yeni feature kayıt dosyasını değiştirmez.
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
