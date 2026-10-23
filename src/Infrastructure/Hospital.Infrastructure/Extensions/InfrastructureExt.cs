using Hospital.Application.Extensions;
using Hospital.Infrastructure.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Infrastructure.Extensions;

/// <summary>Infrastructure katmanının DI kaydı. Program.cs'te <c>builder.Services.AddInfrastructure()</c> ile çağrılır.</summary>
public static class InfrastructureExt
{
    private const string FeaturesNamespace = "Hospital.Infrastructure.Features";

    /// <summary>Veri tabanı dışı dış servisler (parola özeti, e-posta vb.). Persistence ayrı katmandır.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // E-posta outbox işleyicisi (arka plan hizmeti); gönderici ve diğer somutlar aşağıda taranarak kaydedilir.
        services.AddHostedService<EmailOutboxProcessor>();

        return services.AddFeatureImplementations(AssemblyReference.Assembly, FeaturesNamespace);
    }
}
