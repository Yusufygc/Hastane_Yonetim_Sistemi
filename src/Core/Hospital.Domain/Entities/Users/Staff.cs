using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

/// <summary>Sağlık personeli (hemşire, teknisyen vb.). Bir şubeye (ve isteğe bağlı bölüme) bağlıdır; randevu alamaz/vermez.</summary>
public sealed class Staff : User
{
    public const int NameMaxLength = 100;
    public const int TitleMaxLength = 100;

    /// <summary>EF Core materialization için.</summary>
    private Staff()
    {
        FirstName = null!;
        LastName = null!;
        Title = null!;
    }

    private Staff(
        Guid id,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string title,
        Guid hospitalId,
        Guid? departmentId,
        DateTimeOffset now)
        : base(id, email, passwordHash, UserRole.Staff, now)
    {
        FirstName = firstName;
        LastName = lastName;
        Title = title;
        HospitalId = hospitalId;
        DepartmentId = departmentId;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    /// <summary>Hemşire, Teknisyen vb.</summary>
    public string Title { get; private set; }

    public Guid HospitalId { get; private set; }

    public Guid? DepartmentId { get; private set; }

    public override string FullName => $"{FirstName} {LastName}";

    public static Staff Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string title,
        Guid hospitalId,
        Guid? departmentId,
        DateTimeOffset now) =>
        new(
            Guid.CreateVersion7(),
            email,
            passwordHash,
            Guard.NotBlank(firstName, "Ad", NameMaxLength),
            Guard.NotBlank(lastName, "Soyad", NameMaxLength),
            Guard.NotBlank(title, "Ünvan", TitleMaxLength),
            Guard.NotEmpty(hospitalId, "Şube"),
            departmentId,
            now);
}
