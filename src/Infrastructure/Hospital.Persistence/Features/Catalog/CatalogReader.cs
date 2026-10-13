using Hospital.Application.Features.Catalog;
using Hospital.Application.Features.Hospitals;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Catalog;

/// <summary>Anonim katalog: yalnızca aktif kayıtlar, kişisel veri yok.</summary>
internal sealed class CatalogReader(AppDbContext db) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogHospitalDto>> GetHospitalsAsync(CancellationToken cancellationToken)
    {
        var hospitals = await db.Hospitals
            .AsNoTracking()
            .Where(h => h.IsActive)
            .OrderBy(h => h.Name)
            .Select(h => new
            {
                h.Id,
                h.Name,
                h.Address,
                h.Phone,
                h.TimeZoneId,
                WorkingHours = h.WorkingHours.Select(w => new WorkingHoursDto(w.Day, w.OpenTime, w.CloseTime)).ToList(),
            })
            .ToListAsync(cancellationToken);

        return hospitals
            .Select(h => new CatalogHospitalDto(
                h.Id,
                h.Name,
                h.Address,
                h.Phone,
                h.TimeZoneId,
                h.WorkingHours.OrderBy(w => ((int)w.Day + 6) % 7).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogDepartmentDto>?> GetDepartmentsAsync(Guid hospitalId, CancellationToken cancellationToken)
    {
        if (!await IsActiveHospitalAsync(hospitalId, cancellationToken))
        {
            return null;
        }

        return await db.Departments
            .AsNoTracking()
            .Where(d => d.HospitalId == hospitalId && d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new CatalogDepartmentDto(d.Id, d.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogServiceDto>?> GetServicesAsync(Guid hospitalId, CancellationToken cancellationToken)
    {
        if (!await IsActiveHospitalAsync(hospitalId, cancellationToken))
        {
            return null;
        }

        return await db.Services
            .AsNoTracking()
            .Where(s => s.HospitalId == hospitalId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new CatalogServiceDto(s.Id, s.DepartmentId, s.Name, s.DurationMinutes, s.Price, s.Currency))
            .ToListAsync(cancellationToken);
    }

    private Task<bool> IsActiveHospitalAsync(Guid hospitalId, CancellationToken cancellationToken) =>
        db.Hospitals.AsNoTracking().AnyAsync(h => h.Id == hospitalId && h.IsActive, cancellationToken);
}
