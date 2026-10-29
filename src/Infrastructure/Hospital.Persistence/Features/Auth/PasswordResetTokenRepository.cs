using Hospital.Application.Features.Auth;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Auth;

internal sealed class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<PasswordResetToken>> GetUsableByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);
}
