namespace Hospital.Domain.Common;

/// <summary>Aggregate içinde olan önemli bir değişikliği temsil eder (Observer / Outbox kaynağı).</summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
