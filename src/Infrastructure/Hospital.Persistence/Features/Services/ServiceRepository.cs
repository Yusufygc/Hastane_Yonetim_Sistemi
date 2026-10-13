using Hospital.Application.Features.Services;
using Hospital.Domain.Entities.Facility;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Services;

internal sealed class ServiceRepository(AppDbContext db) : IServiceRepository
{
    public Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid hospitalId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();

        return db.Services.AnyAsync(
            s => s.HospitalId == hospitalId && s.Name.ToLower() == lowered && s.Id != excludeId,
            cancellationToken);
    }

    public void Add(Service service) => db.Services.Add(service);
}
