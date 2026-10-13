using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Auth;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> PatientIdentityNumberExistsAsync(string identityNumber, CancellationToken cancellationToken);

    void Add(User user);
}

/// <summary>Her istekte erişim belirtecinin hâlâ geçerli olup olmadığını denetler (okuma tarafı).</summary>
public interface IAccountSessionReader
{
    Task<bool> IsSessionValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Kullanıcının süresi dolmamış ve iptal edilmemiş tüm belirteçleri (çalınma şüphesinde topluca iptal için).</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}
