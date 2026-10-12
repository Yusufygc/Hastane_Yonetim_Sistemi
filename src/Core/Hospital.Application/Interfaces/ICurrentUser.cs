namespace Hospital.Application.Interfaces;

/// <summary>İstek sahibi kullanıcı (cookie biletindeki claim'lerden).</summary>
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
