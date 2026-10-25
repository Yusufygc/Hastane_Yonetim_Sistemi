using Hospital.Application.Features.Auth;
using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Auth;

internal sealed class UserRepository(AppDbContext db, ISensitiveDataProtector protector) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> PatientIdentityNumberExistsAsync(string identityNumber, CancellationToken cancellationToken)
    {
        // Sütun şifreli olduğundan eşitlik şifre çözülmeden, deterministik kör indeksle aranır.
        var hash = protector.BlindIndex(identityNumber, SensitiveDataPurposes.PatientIdentityNumber);
        return db.Patients.AnyAsync(p => EF.Property<string>(p, AppDbContext.PatientIdentityHash) == hash, cancellationToken);
    }

    public void Add(User user) => db.Users.Add(user);
}
