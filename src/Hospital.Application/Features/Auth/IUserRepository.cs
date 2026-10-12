using Hospital.Domain.Users;

namespace Hospital.Application.Features.Auth;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> PatientIdentityNumberExistsAsync(string identityNumber, CancellationToken cancellationToken);

    void Add(User user);
}

/// <summary>Her istekte cookie oturumunun hâlâ geçerli olup olmadığını denetler (okuma tarafı).</summary>
public interface IAccountSessionReader
{
    Task<bool> IsSessionValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);
}
