using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Facility;

/// <summary>
/// Kurum (zincir). Her zaman vardır: tek şubeli kurulum 1 organizasyon + 1 şube, çok şubeli kurulum aynı
/// organizasyona yeni şubeler ekler.
/// </summary>
public sealed class Organization : AggregateRoot
{
    public const int NameMaxLength = 200;

    /// <summary>EF Core materialization için.</summary>
    private Organization()
    {
        Name = null!;
    }

    private Organization(Guid id, string name) : base(id)
    {
        Name = name;
        IsActive = true;
    }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public static Organization Create(string name) =>
        new(Guid.CreateVersion7(), Guard.NotBlank(name, "Kurum adı", NameMaxLength));

    public void Rename(string name) => Name = Guard.NotBlank(name, "Kurum adı", NameMaxLength);
}
