using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Facility;

/// <summary>Şubeye bağlı bölüm (ör. Kardiyoloji). Silinmez, pasifleştirilir.</summary>
public sealed class Department : AggregateRoot
{
    public const int NameMaxLength = 150;

    /// <summary>EF Core materialization için.</summary>
    private Department()
    {
        Name = null!;
    }

    private Department(Guid id, Guid hospitalId, string name) : base(id)
    {
        HospitalId = hospitalId;
        Name = name;
        IsActive = true;
    }

    public Guid HospitalId { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public static Department Create(Guid hospitalId, string name) =>
        new(Guid.CreateVersion7(), Guard.NotEmpty(hospitalId, "Şube"), Guard.NotBlank(name, "Bölüm adı", NameMaxLength));

    public void Rename(string name) => Name = Guard.NotBlank(name, "Bölüm adı", NameMaxLength);

    public void Deactivate() => IsActive = false;
}
