using FluentValidation;
using Hospital.Application.Behaviors;
using Hospital.Application.Features.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Application.Extensions;

/// <summary>Application katmanının DI kaydı. Program.cs'te <c>builder.Services.AddApplication()</c> ile çağrılır.</summary>
public static class ApplicationExt
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

        // Login/Refresh komutlarının ortak belirteç üretimi.
        services.AddScoped<AuthTokenIssuer>();

        return services;
    }
}
