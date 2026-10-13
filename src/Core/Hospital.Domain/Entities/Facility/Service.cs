using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Facility;

/// <summary>Şubenin sunduğu sağlık hizmeti: sabit süre ve ücret. Silinmez, pasifleştirilir.</summary>
public sealed class Service : AggregateRoot
{
    public const int NameMaxLength = 200;
    public const int MaxDurationMinutes = 480;
    public const decimal MaxPrice = 9_999_999_999.99m; // numeric(12,2)

    /// <summary>EF Core materialization için.</summary>
    private Service()
    {
        Name = null!;
        Currency = null!;
    }

    private Service(
        Guid id,
        Guid hospitalId,
        Guid? departmentId,
        string name,
        int durationMinutes,
        decimal price,
        string currency)
        : base(id)
    {
        HospitalId = hospitalId;
        DepartmentId = departmentId;
        Name = name;
        DurationMinutes = durationMinutes;
        Price = price;
        Currency = currency;
        IsActive = true;
    }

    public Guid HospitalId { get; private set; }

    public Guid? DepartmentId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Sabit hizmet süresi (dakika). Slot uzunluğunu belirler.</summary>
    public int DurationMinutes { get; private set; }

    public decimal Price { get; private set; }

    /// <summary>ISO 4217 para birimi (ör. TRY).</summary>
    public string Currency { get; private set; }

    public bool IsActive { get; private set; }

    public static Service Create(
        Guid hospitalId,
        Guid? departmentId,
        string name,
        int durationMinutes,
        decimal price,
        string currency) =>
        new(
            Guid.CreateVersion7(),
            Guard.NotEmpty(hospitalId, "Şube"),
            departmentId,
            Guard.NotBlank(name, "Hizmet adı", NameMaxLength),
            ValidateDuration(durationMinutes),
            ValidatePrice(price),
            ValidateCurrency(currency));

    public void Update(Guid? departmentId, string name, int durationMinutes, decimal price, string currency)
    {
        DepartmentId = departmentId;
        Name = Guard.NotBlank(name, "Hizmet adı", NameMaxLength);
        DurationMinutes = ValidateDuration(durationMinutes);
        Price = ValidatePrice(price);
        Currency = ValidateCurrency(currency);
    }

    public void Deactivate() => IsActive = false;

    private static int ValidateDuration(int minutes) =>
        minutes is > 0 and <= MaxDurationMinutes
            ? minutes
            : throw new DomainException($"Hizmet süresi 1 ile {MaxDurationMinutes} dakika arasında olmalıdır.");

    private static decimal ValidatePrice(decimal price) =>
        price is >= 0 and <= MaxPrice
            ? decimal.Round(price, 2)
            : throw new DomainException("Ücret 0'dan küçük olamaz.");

    private static string ValidateCurrency(string? currency)
    {
        var value = Guard.NotBlank(currency, "Para birimi", 3).ToUpperInvariant();

        return value.Length == 3 && value.All(char.IsAsciiLetter)
            ? value
            : throw new DomainException("Para birimi 3 harfli ISO kodu olmalıdır (ör. TRY).");
    }
}
