using Hospital.Application.Features.Auth;
using Hospital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Infrastructure.Features.Auth;

internal sealed class AccountSessionReader(AppDbContext db) : IAccountSessionReader
{
    public Task<bool> IsSessionValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == securityStamp, cancellationToken);
}
