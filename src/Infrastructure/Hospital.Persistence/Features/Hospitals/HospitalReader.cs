using Hospital.Application.Features.Hospitals;
using Hospital.Application.Security;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Hospitals;

internal sealed class HospitalReader(AppDbContext db) : IHospitalReader
{
    public async Task<IReadOnlyList<HospitalDto>> ListAsync(AdminScope scope, CancellationToken cancellationToken) =>
        await db.Hospitals
            .AsNoTracking()
            .Where(h => h.OrganizationId == scope.OrganizationId && (scope.HospitalId == null || h.Id == scope.HospitalId))
            .OrderBy(h => h.Name)
            .Select(h => new HospitalDto(h.Id, h.OrganizationId, h.Name, h.Address, h.Phone, h.TimeZoneId, h.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<HospitalDetailDto?> GetDetailAsync(Guid id, AdminScope scope, CancellationToken cancellationToken)
    {
        var hospital = await db.Hospitals
            .AsNoTracking()
            .Where(h => h.Id == id
                        && h.OrganizationId == scope.OrganizationId
                        && (scope.HospitalId == null || h.Id == scope.HospitalId))
            .Select(h => new
            {
                h.Id,
                h.OrganizationId,
                h.Name,
                h.Address,
                h.Phone,
                h.TimeZoneId,
                h.IsActive,
                WorkingHours = h.WorkingHours.Select(w => new WorkingHoursDto(w.Day, w.OpenTime, w.CloseTime)).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (hospital is null)
        {
            return null;
        }

        // Pazartesi'den başlayan sıra (0 = Pazar en sona).
        var hours = hospital.WorkingHours.OrderBy(w => ((int)w.Day + 6) % 7).ToList();

        return new HospitalDetailDto(
            hospital.Id,
            hospital.OrganizationId,
            hospital.Name,
            hospital.Address,
            hospital.Phone,
            hospital.TimeZoneId,
            hospital.IsActive,
            hours);
    }
}
