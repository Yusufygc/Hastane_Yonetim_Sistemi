using Hospital.Application.Features.Catalog;
using Hospital.Application.Features.Hospitals;
using Hospital.Domain.Entities.Users;
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

    public async Task<IReadOnlyList<CatalogDoctorDto>?> GetDoctorsAsync(
        Guid hospitalId,
        Guid? serviceId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveHospitalAsync(hospitalId, cancellationToken))
        {
            return null;
        }

        var doctors = await (
                from d in db.Doctors.AsNoTracking()
                join dep in db.Departments on d.DepartmentId equals dep.Id
                where dep.HospitalId == hospitalId && dep.IsActive && d.IsActive
                orderby d.LastName, d.FirstName
                select new { d.Id, d.FirstName, d.LastName, d.Specialty, DepartmentId = dep.Id, DepartmentName = dep.Name })
            .ToListAsync(cancellationToken);

        var doctorIds = doctors.Select(d => d.Id).ToList();

        // Yalnızca yönetici onaylı ve aktif hizmetler randevu alınabilirliğini belirler.
        var links = await (
                from l in db.Set<DoctorServiceLink>().AsNoTracking()
                join s in db.Services on l.ServiceId equals s.Id
                where doctorIds.Contains(l.DoctorId) && l.Status == DoctorServiceStatus.Approved && s.IsActive
                orderby s.Name
                select new { l.DoctorId, s.Id, s.Name, s.DurationMinutes, s.Price, s.Currency })
            .ToListAsync(cancellationToken);

        var servicesByDoctor = links
            .GroupBy(l => l.DoctorId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CatalogDoctorServiceDto>)g
                    .Select(l => new CatalogDoctorServiceDto(l.Id, l.Name, l.DurationMinutes, l.Price, l.Currency))
                    .ToList());

        return doctors
            .Where(d => servicesByDoctor.TryGetValue(d.Id, out var offered)
                        && (serviceId is null || offered.Any(s => s.Id == serviceId)))
            .Select(d => new CatalogDoctorDto(
                d.Id, d.FirstName, d.LastName, d.Specialty, d.DepartmentId, d.DepartmentName, servicesByDoctor[d.Id]))
            .ToList();
    }

    private Task<bool> IsActiveHospitalAsync(Guid hospitalId, CancellationToken cancellationToken) =>
        db.Hospitals.AsNoTracking().AnyAsync(h => h.Id == hospitalId && h.IsActive, cancellationToken);
}
