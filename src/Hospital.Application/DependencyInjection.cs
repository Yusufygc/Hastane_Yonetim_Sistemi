using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handler'lar Mediator source generator ile derleme zamanında bulunur (Features/** altı).
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        // Validator'lar da feature klasörlerinden taranarak kaydedilir.
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        return services;
    }
}
