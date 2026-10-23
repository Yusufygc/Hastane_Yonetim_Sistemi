using FluentValidation;
using Hospital.Application.Interfaces;
using Hospital.Application.Behaviors;
using Hospital.Application.Features.Appointments;
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

        // Slot sorgusu ve randevu oluşturmanın ortak ön koşul + müsaitlik hesabı.
        services.AddScoped<SlotAvailabilityService>();

        // Domain event dinleyicileri (IDomainEventHandler<>) Features/** altından taranıp kaydedilir.
        services.AddDomainEventHandlers();

        return services;
    }

    /// <summary>Application assembly'sindeki tüm <see cref="IDomainEventHandler{TEvent}"/> uygulamalarını kaydeder.</summary>
    private static IServiceCollection AddDomainEventHandlers(this IServiceCollection services)
    {
        var handlerContract = typeof(IDomainEventHandler<>);

        foreach (var type in AssemblyReference.Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contract in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerContract))
            {
                services.AddScoped(contract, type);
            }
        }

        return services;
    }
}
