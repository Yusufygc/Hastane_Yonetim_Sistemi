using Hospital.Domain.Common;

namespace Hospital.Application.Interfaces;

/// <summary>
/// Domain event dinleyicisi (Observer). <c>SaveChanges</c> sırasında, değişiklikler yazılmadan önce çağrılır ve
/// <b>aynı DbContext/transaction</b> içinde çalışır: handler'ın eklediği kayıtlar (ör. e-posta outbox satırı) olayı
/// üreten değişiklikle birlikte commit olur veya birlikte geri alınır. Handler ağ çağrısı yapmamalıdır.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Olayı kayıtlı tüm <see cref="IDomainEventHandler{TEvent}"/> dinleyicilerine dağıtır.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
