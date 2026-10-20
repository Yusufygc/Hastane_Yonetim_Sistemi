using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

public sealed class Patient : User
{
    /// <summary>EF Core materialization için.</summary>
    private Patient()
    {
        FirstName = null!;
        LastName = null!;
        IdentityNumber = null!;
    }

    private Patient(
        Guid id,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string identityNumber,
        DateOnly birthDate,
        string? phone,
        DateTimeOffset now)
        : base(id, email, passwordHash, UserRole.Patient, now)
    {
        FirstName = firstName;
        LastName = lastName;
        IdentityNumber = identityNumber;
        BirthDate = birthDate;
        Phone = phone;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string IdentityNumber { get; private set; }

    public DateOnly BirthDate { get; private set; }

    public string? BloodType { get; private set; }

    public string? Phone { get; private set; }

    public override string FullName => $"{FirstName} {LastName}";

    public static readonly IReadOnlySet<string> BloodTypes =
        new HashSet<string>(["A+", "A-", "B+", "B-", "AB+", "AB-", "0+", "0-"]);

    /// <summary>Hastanın kendi profilinde değiştirebildikleri (T.C. ve doğum tarihi değiştirilemez).</summary>
    public void UpdateProfile(string firstName, string lastName, string? phone, string? bloodType)
    {
        FirstName = Guard.NotBlank(firstName, "Ad", 100);
        LastName = Guard.NotBlank(lastName, "Soyad", 100);
        Phone = Guard.OptionalText(phone, "Telefon", 32);

        var normalized = string.IsNullOrWhiteSpace(bloodType) ? null : bloodType.Trim().ToUpperInvariant();
        if (normalized is not null && !BloodTypes.Contains(normalized))
        {
            throw new DomainException("Kan grubu geçersiz (A+, A-, B+, B-, AB+, AB-, 0+, 0-).");
        }

        BloodType = normalized;
    }

    public static Patient Register(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string identityNumber,
        DateOnly birthDate,
        string? phone,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Ad ve soyad boş olamaz.");
        }

        if (!TurkishIdentityNumber.IsValid(identityNumber))
        {
            throw new DomainException("T.C. kimlik numarası geçersiz.");
        }

        if (birthDate > DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new DomainException("Doğum tarihi gelecekte olamaz.");
        }

        return new Patient(
            Guid.CreateVersion7(),
            email,
            passwordHash,
            firstName.Trim(),
            lastName.Trim(),
            identityNumber,
            birthDate,
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            now);
    }
}
