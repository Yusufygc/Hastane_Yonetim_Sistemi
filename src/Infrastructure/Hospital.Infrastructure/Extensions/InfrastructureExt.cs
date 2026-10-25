using Hospital.Application.Extensions;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Infrastructure.Security;
using Hospital.Infrastructure.Features.Appointments;
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
        // Hassas veri (T.C. kimlik no vb.) şifreleme: tek örnek; EF modeli bu örneği yakalar. Anahtar eksik/hatalıysa kurulurken hata verir.
        services.AddSingleton<ISensitiveDataProtector>(provider =>
            new AesGcmSensitiveDataProtector(provider.GetRequiredService<DataProtectionSettings>()));

        services.AddHostedService<EmailOutboxProcessor>();

        // Doktorun karar vermediği bekleyen randevu taleplerini otomatik düşüren arka plan işi.
        services.AddHostedService<PendingAppointmentExpiryProcessor>();

        return services.AddFeatureImplementations(AssemblyReference.Assembly, FeaturesNamespace);
    }
}
