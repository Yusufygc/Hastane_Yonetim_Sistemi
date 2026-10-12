namespace Hospital.Domain.Common;

/// <summary>Kimliğiyle ayırt edilen domain nesnesi. Kimlikler Guid v7 (zamana göre sıralı).</summary>
public abstract class Entity : IEquatable<Entity>
{
    /// <summary>EF Core materialization için.</summary>
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Kimlik boş olamaz.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private set; }

    public bool Equals(Entity? other) =>
        other is not null && GetType() == other.GetType() && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as Entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
