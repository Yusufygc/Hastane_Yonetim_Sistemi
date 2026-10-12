using Hospital.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Infrastructure.Extensions;

/// <summary>Infrastructure katmanının DI kaydı. Program.cs'te <c>builder.Services.AddInfrastructure()</c> ile çağrılır.</summary>
public static class InfrastructureExt
{
    private const string FeaturesNamespace = "Hospital.Infrastructure.Features";

    /// <summary>Veri tabanı dışı dış servisler (parola özeti, e-posta vb.). Persistence ayrı katmandır.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services.AddFeatureImplementations(AssemblyReference.Assembly, FeaturesNamespace);
}
