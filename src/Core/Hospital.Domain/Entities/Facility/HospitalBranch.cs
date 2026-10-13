using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Facility;

/// <summary>
/// Hastane/klinik şubesi (ER'deki HOSPITAL, tablo adı <c>hospital</c>). Sınıf adı kök namespace
/// <c>Hospital</c> ile çakışmasın diye <c>HospitalBranch</c>'tir.
/// </summary>
public sealed class HospitalBranch : AggregateRoot
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 500;
    public const int PhoneMaxLength = 32;

    private readonly List<HospitalWorkingHours> _workingHours = [];

    /// <summary>EF Core materialization için.</summary>
    private HospitalBranch()
    {
        Name = null!;
        TimeZoneId = null!;
    }

    private HospitalBranch(Guid id, Guid organizationId, string name, string? address, string? phone, string timeZoneId)
        : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
        Address = address;
        Phone = phone;
        TimeZoneId = timeZoneId;
        IsActive = true;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public string? Address { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>IANA saat dilimi (ör. Europe/Istanbul). Haftalık saatler bu dilimde yorumlanır.</summary>
    public string TimeZoneId { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<HospitalWorkingHours> WorkingHours => _workingHours;

    public static HospitalBranch Create(Guid organizationId, string name, string? address, string? phone, string timeZoneId) =>
        new(
            Guid.CreateVersion7(),
            Guard.NotEmpty(organizationId, "Kurum"),
            Guard.NotBlank(name, "Şube adı", NameMaxLength),
            Guard.OptionalText(address, "Adres", AddressMaxLength),
            Guard.OptionalText(phone, "Telefon", PhoneMaxLength),
            ValidateTimeZone(timeZoneId));

    public void Update(string name, string? address, string? phone, string timeZoneId)
    {
        Name = Guard.NotBlank(name, "Şube adı", NameMaxLength);
        Address = Guard.OptionalText(address, "Adres", AddressMaxLength);
        Phone = Guard.OptionalText(phone, "Telefon", PhoneMaxLength);
        TimeZoneId = ValidateTimeZone(timeZoneId);
    }

    /// <summary>
    /// Haftalık çalışma saatlerini verilen liste ile eşitler: mevcut günler güncellenir, yeni günler eklenir,
    /// listede olmayan günler kaldırılır (kapalı sayılır). Aynı güne birden fazla aralık verilemez.
    /// </summary>
    public void SetWorkingHours(IEnumerable<WorkingHoursInput> hours)
    {
        var inputs = hours.ToList();

        var duplicate = inputs.GroupBy(h => h.Day).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainException($"{duplicate.Key} günü için birden fazla çalışma aralığı verilemez.");
        }

        // Önce doğrula (hiçbir değişiklik yapmadan), sonra uygula.
        if (inputs.Any(i => i.CloseTime <= i.OpenTime))
        {
            var invalid = inputs.First(i => i.CloseTime <= i.OpenTime);
            throw new DomainException($"{invalid.Day}: kapanış saati açılış saatinden sonra olmalıdır.");
        }

        _workingHours.RemoveAll(existing => inputs.All(i => i.Day != existing.Day));

        foreach (var input in inputs)
        {
            var existing = _workingHours.FirstOrDefault(w => w.Day == input.Day);
            if (existing is null)
            {
                _workingHours.Add(new HospitalWorkingHours(Id, input.Day, input.OpenTime, input.CloseTime));
            }
            else
            {
                existing.Set(input.OpenTime, input.CloseTime);
            }
        }
    }

    public void Deactivate() => IsActive = false;

    private static string ValidateTimeZone(string? timeZoneId)
    {
        var value = Guard.NotBlank(timeZoneId, "Saat dilimi", 64);

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new DomainException($"Geçersiz saat dilimi: {value}.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new DomainException($"Geçersiz saat dilimi: {value}.");
        }

        return value;
    }
}
