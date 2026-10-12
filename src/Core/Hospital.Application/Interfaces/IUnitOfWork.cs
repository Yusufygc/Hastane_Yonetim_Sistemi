namespace Hospital.Application.Interfaces;

/// <summary>Unit of Work: handler tek SaveChanges ile tüm değişiklikleri atomik yazar.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
