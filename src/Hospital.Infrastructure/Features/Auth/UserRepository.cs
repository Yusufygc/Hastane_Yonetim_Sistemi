using Hospital.Application.Features.Auth;
using Hospital.Domain.Users;
using Hospital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Infrastructure.Features.Auth;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> PatientIdentityNumberExistsAsync(string identityNumber, CancellationToken cancellationToken) =>
        db.Patients.AnyAsync(p => p.IdentityNumber == identityNumber, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}
