using Hospital.Application.Features.Departments;
using Hospital.Domain.Entities.Facility;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Departments;

internal sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(Guid hospitalId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        // Kültürden bağımsız: tr-TR'de ToLower() "I" → "ı" yapar, veritabanı lower() ise "i"; karşılaştırma tutmaz.
        var lowered = name.ToLowerInvariant();

        return db.Departments.AnyAsync(
            d => d.HospitalId == hospitalId && d.Name.ToLower() == lowered && d.Id != excludeId,
            cancellationToken);
    }

    public Task<bool> HasActiveServicesAsync(Guid departmentId, CancellationToken cancellationToken) =>
        db.Services.AnyAsync(s => s.DepartmentId == departmentId && s.IsActive, cancellationToken);

    public void Add(Department department) => db.Departments.Add(department);
}
