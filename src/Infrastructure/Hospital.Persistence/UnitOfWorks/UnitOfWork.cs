using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;

namespace Hospital.Persistence.UnitOfWorks;

/// <summary>
/// Unit of Work = DbContext: handler'ın tüm değişiklikleri tek transaction'da atomik yazmasını sağlar.
/// Kısıt ihlali çevirisi (DataConflictException) AppDbContext.SaveChangesAsync içindedir.
/// </summary>
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
