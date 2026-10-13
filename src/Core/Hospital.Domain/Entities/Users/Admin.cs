using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

public enum AdminAccessLevel
{
    /// <summary>Organizasyondaki tüm şubeleri yönetir (<see cref="Admin.HospitalId"/> boştur).</summary>
    SuperAdmin = 1,

    /// <summary>Yalnızca atandığı şubeyi yönetir.</summary>
    BranchManager = 2,
}

public static class AdminAccessLevelNames
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string BranchManager = "BRANCH_MANAGER";

    public static string From(AdminAccessLevel level) => level switch
    {
        AdminAccessLevel.SuperAdmin => SuperAdmin,
        AdminAccessLevel.BranchManager => BranchManager,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public static AdminAccessLevel Parse(string value) => value switch
    {
        SuperAdmin => AdminAccessLevel.SuperAdmin,
        BranchManager => AdminAccessLevel.BranchManager,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Bilinmeyen erişim seviyesi."),
    };
}

/// <summary>Hastane/klinik yöneticisi. Kapsamı (organizasyon veya tek şube) cookie claim'lerine yansır.</summary>
public sealed class Admin : User
{
    /// <summary>EF Core materialization için.</summary>
    private Admin()
    {
        FirstName = null!;
        LastName = null!;
    }

    private Admin(
        Guid id,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        AdminAccessLevel accessLevel,
        Guid organizationId,
        Guid? hospitalId,
        DateTimeOffset now)
        : base(id, email, passwordHash, UserRole.Admin, now)
    {
        FirstName = firstName;
        LastName = lastName;
        AccessLevel = accessLevel;
        OrganizationId = organizationId;
        HospitalId = hospitalId;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public AdminAccessLevel AccessLevel { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>Şube yöneticisi için atandığı şube; süper yönetici için boş.</summary>
    public Guid? HospitalId { get; private set; }

    public override string FullName => $"{FirstName} {LastName}";

    public override IReadOnlyDictionary<string, string> ScopeClaims
    {
        get
        {
            var claims = new Dictionary<string, string>
            {
                [ScopeClaimTypes.OrganizationId] = OrganizationId.ToString(),
                [ScopeClaimTypes.AccessLevel] = AdminAccessLevelNames.From(AccessLevel),
            };

            if (HospitalId is { } hospitalId)
            {
                claims[ScopeClaimTypes.HospitalId] = hospitalId.ToString();
            }

            return claims;
        }
    }

    public static Admin CreateSuperAdmin(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        Guid organizationId,
        DateTimeOffset now) =>
        new(
            Guid.CreateVersion7(),
            email,
            passwordHash,
            Guard.NotBlank(firstName, "Ad", 100),
            Guard.NotBlank(lastName, "Soyad", 100),
            AdminAccessLevel.SuperAdmin,
            Guard.NotEmpty(organizationId, "Kurum"),
            null,
            now);

    public static Admin CreateBranchManager(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        Guid organizationId,
        Guid hospitalId,
        DateTimeOffset now) =>
        new(
            Guid.CreateVersion7(),
            email,
            passwordHash,
            Guard.NotBlank(firstName, "Ad", 100),
            Guard.NotBlank(lastName, "Soyad", 100),
            AdminAccessLevel.BranchManager,
            Guard.NotEmpty(organizationId, "Kurum"),
            Guard.NotEmpty(hospitalId, "Şube"),
            now);
}
