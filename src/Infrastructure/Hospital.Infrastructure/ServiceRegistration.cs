using Hospital.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Infrastructure;

public static class ServiceRegistration
{
    private const string FeaturesNamespace = "Hospital.Infrastructure.Features";

    /// <summary>Dış servisler (parola özeti, e-posta vb.). Persistence ayrı katmandır.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services.AddFeatureImplementations(AssemblyReference.Assembly, FeaturesNamespace);
}
