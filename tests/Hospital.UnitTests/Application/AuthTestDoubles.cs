using Hospital.Application.Features.Auth;
using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Application;

/// <summary>Auth handler testlerinde kullanılan sahte bağımlılıklar.</summary>
internal sealed class FakeUsers : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Email == normalizedEmail));

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(u => u.Email == normalizedEmail));

    public Task<bool> PatientIdentityNumberExistsAsync(string identityNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Items.OfType<Patient>().Any(p => p.IdentityNumber == identityNumber));

    public void Add(User user) => Items.Add(user);
}

internal sealed class FakeRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.UserId == userId && t.IsActive(now)).ToList());

    public void Add(RefreshToken token) => Items.Add(token);
}

internal sealed class FakeHasher : IPasswordHasher
{
    public int HashCalls { get; private set; }

    public string Hash(string password)
    {
        HashCalls++;
        return $"hash:{password}";
    }

    public bool Verify(string passwordHash, string password) => passwordHash == $"hash:{password}";
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeTokenService : ITokenService
{
    public int Created { get; private set; }

    public AccessToken CreateAccessToken(AuthenticatedUser user, DateTimeOffset now)
    {
        Created++;
        return new AccessToken($"jwt-for-{user.Id}-{Created}", now.AddMinutes(15));
    }
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
