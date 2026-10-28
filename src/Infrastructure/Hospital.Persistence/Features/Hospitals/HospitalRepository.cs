using Hospital.Application.Features.Hospitals;
using Hospital.Domain.Entities.Facility;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Hospitals;

internal sealed class HospitalRepository(AppDbContext db) : IHospitalRepository
{
    public Task<HospitalBranch?> GetByIdAsync(Guid id, bool includeWorkingHours, CancellationToken cancellationToken)
    {
        IQueryable<HospitalBranch> query = db.Hospitals;

        if (includeWorkingHours)
        {
            query = query.Include(h => h.WorkingHours);
        }

        return query.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        // Kültürden bağımsız: tr-TR'de ToLower() "I" → "ı" yapar, veritabanı lower() ise "i"; karşılaştırma tutmaz.
        var lowered = name.ToLowerInvariant();

        return db.Hospitals.AnyAsync(
            h => h.OrganizationId == organizationId && h.Name.ToLower() == lowered && h.Id != excludeId,
            cancellationToken);
    }

    public void Add(HospitalBranch hospital) => db.Hospitals.Add(hospital);
}
