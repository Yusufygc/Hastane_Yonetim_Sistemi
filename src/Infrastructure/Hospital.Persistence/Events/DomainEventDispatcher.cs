using System.Reflection;
using Hospital.Application.Interfaces;
using Hospital.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.Persistence.Events;

/// <summary>
/// Olayı kendi türüne kayıtlı tüm <see cref="IDomainEventHandler{TEvent}"/> dinleyicilerine sırayla dağıtır.
/// Dinleyiciler çağrı anında çözülür (kurucuda değil): <c>AppDbContext</c> bu sınıfa bağlıdır, dinleyiciler ise
/// <c>AppDbContext</c>'e; kurucuda çözmek döngüsel bağımlılık olurdu.
/// </summary>
internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var contract = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var method = contract.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        foreach (var handler in serviceProvider.GetServices(contract))
        {
            try
            {
                await (Task)method.Invoke(handler, [domainEvent, cancellationToken])!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Yansıma sarmalını kaldır: gerçek istisna (ör. DataConflictException) olduğu gibi yükselsin.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}
