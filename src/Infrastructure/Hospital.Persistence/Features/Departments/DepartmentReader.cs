using Hospital.Application.Features.Departments;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Departments;

internal sealed class DepartmentReader(AppDbContext db) : IDepartmentReader
{
    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(Guid hospitalId, CancellationToken cancellationToken) =>
        await db.Departments
            .AsNoTracking()
            .Where(d => d.HospitalId == hospitalId)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.HospitalId, d.Name, d.IsActive))
            .ToListAsync(cancellationToken);
}
