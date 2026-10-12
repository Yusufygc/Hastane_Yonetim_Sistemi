namespace Hospital.Application.Common.Abstractions;

/// <summary>Unit of Work: handler tek SaveChanges ile tüm değişiklikleri atomik yazar.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>İstek sahibi kullanıcı (cookie bileti claim'lerinden).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    /// <summary>PATIENT, DOCTOR, STAFF veya ADMIN.</summary>
    string? Role { get; }

    Guid? OrganizationId { get; }

    Guid? HospitalId { get; }

    /// <summary>Yöneticiler için SUPER_ADMIN veya BRANCH_MANAGER.</summary>
    string? AccessLevel { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
