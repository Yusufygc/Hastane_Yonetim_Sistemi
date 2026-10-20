using Hospital.Application.Features.Doctors;
using Hospital.Application.Security;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Hospital.Persistence.Features.Doctors;

internal sealed class DoctorReader(AppDbContext db) : IDoctorReader
{
    public async Task<IReadOnlyList<DoctorDto>> ListAsync(
        AdminScope scope,
        Guid? hospitalId,
        Guid? departmentId,
        CancellationToken cancellationToken) =>
        await InScope(scope)
            .Where(x => (hospitalId == null || x.HospitalId == hospitalId)
                        && (departmentId == null || x.DepartmentId == departmentId))
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    public async Task<DoctorDetailDto?> GetAsync(Guid id, AdminScope scope, CancellationToken cancellationToken)
    {
        var dto = await InScope(scope)
            .Where(x => x.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null ? null : ToDetail(dto, await ListServicesAsync(id, cancellationToken));
    }

    public async Task<DoctorDetailDto?> GetOwnAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var dto = await AllDoctors()
            .Where(x => x.Id == doctorId)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null ? null : ToDetail(dto, await ListServicesAsync(doctorId, cancellationToken));
    }

    public async Task<IReadOnlyList<DoctorServiceDto>> ListServicesAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var rows = await (
                from l in db.Set<DoctorServiceLink>().AsNoTracking()
                join s in db.Services on l.ServiceId equals s.Id
                where l.DoctorId == doctorId
                orderby s.Name
                select new { l.ServiceId, ServiceName = s.Name, l.Status, l.CreatedAt, l.DecidedAt })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DoctorServiceDto(r.ServiceId, r.ServiceName, DoctorServiceStatusNames.From(r.Status), r.CreatedAt, r.DecidedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<DoctorAvailabilityDto>> ListAvailabilitiesAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var rows = await db.Set<DoctorAvailability>()
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId)
            .Select(a => new DoctorAvailabilityDto(a.Id, a.Day, a.StartTime, a.EndTime, a.ValidFrom, a.ValidTo))
            .ToListAsync(cancellationToken);

        // Pazartesi'den başlayan hafta sırası, sonra saat.
        return rows
            .OrderBy(a => ((int)a.Day + 6) % 7)
            .ThenBy(a => a.StartTime)
            .ThenBy(a => a.ValidFrom)
            .ToList();
    }

    public async Task<IReadOnlyList<DoctorTimeOffDto>> ListTimeOffsAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await db.Set<DoctorTimeOff>()
            .AsNoTracking()
            .Where(t => t.DoctorId == doctorId)
            .OrderBy(t => t.StartAt)
            .Select(t => new DoctorTimeOffDto(t.Id, t.StartAt, t.EndAt, t.Reason))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorServiceRequestDto>> ListServiceRequestsAsync(
        AdminScope scope,
        string status,
        CancellationToken cancellationToken)
    {
        var wanted = Enum.Parse<DoctorServiceStatus>(status, ignoreCase: true);

        var rows = await (
                from l in db.Set<DoctorServiceLink>().AsNoTracking()
                join d in db.Doctors on l.DoctorId equals d.Id
                join s in db.Services on l.ServiceId equals s.Id
                join h in db.Hospitals on s.HospitalId equals h.Id
                where l.Status == wanted
                      && h.OrganizationId == scope.OrganizationId
                      && (scope.HospitalId == null || h.Id == scope.HospitalId)
                orderby l.CreatedAt
                select new
                {
                    l.DoctorId,
                    d.FirstName,
                    d.LastName,
                    HospitalId = h.Id,
                    l.ServiceId,
                    ServiceName = s.Name,
                    l.Status,
                    l.CreatedAt,
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DoctorServiceRequestDto(
                r.DoctorId,
                $"{r.FirstName} {r.LastName}",
                r.HospitalId,
                r.ServiceId,
                r.ServiceName,
                DoctorServiceStatusNames.From(r.Status),
                r.CreatedAt))
            .ToList();
    }

    /// <summary>Doktor + bölüm + şube birleşiminin düz satırı (EF'in üzerinde süzebilmesi için nesne başlatıcılı).</summary>
    private sealed class Row
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = null!;
        public string FirstName { get; init; } = null!;
        public string LastName { get; init; } = null!;
        public Guid OrganizationId { get; init; }
        public Guid HospitalId { get; init; }
        public Guid DepartmentId { get; init; }
        public string DepartmentName { get; init; } = null!;
        public string Specialty { get; init; } = null!;
        public string DiplomaNo { get; init; } = null!;
        public string? Phone { get; init; }
        public bool IsActive { get; init; }
    }

    private static readonly Expression<Func<Row, DoctorDto>> ToDto = x =>
        new DoctorDto(x.Id, x.Email, x.FirstName, x.LastName, x.HospitalId, x.DepartmentId, x.DepartmentName, x.Specialty, x.DiplomaNo, x.Phone, x.IsActive);

    /// <summary>Doktor + bölüm + şube birleşimi (kapsam süzmesi olmadan).</summary>
    private IQueryable<Row> AllDoctors() =>
        from d in db.Doctors.AsNoTracking()
        join dep in db.Departments on d.DepartmentId equals dep.Id
        join h in db.Hospitals on dep.HospitalId equals h.Id
        select new Row
        {
            Id = d.Id,
            Email = d.Email,
            FirstName = d.FirstName,
            LastName = d.LastName,
            OrganizationId = h.OrganizationId,
            HospitalId = h.Id,
            DepartmentId = dep.Id,
            DepartmentName = dep.Name,
            Specialty = d.Specialty,
            DiplomaNo = d.DiplomaNo,
            Phone = d.Phone,
            IsActive = d.IsActive,
        };

    /// <summary>Yalnızca yöneticinin kapsamındaki şubelerin doktorları.</summary>
    private IQueryable<Row> InScope(AdminScope scope) =>
        AllDoctors().Where(x => x.OrganizationId == scope.OrganizationId
                                && (scope.HospitalId == null || x.HospitalId == scope.HospitalId));

    private static DoctorDetailDto ToDetail(DoctorDto d, IReadOnlyList<DoctorServiceDto> services) =>
        new(d.Id, d.Email, d.FirstName, d.LastName, d.HospitalId, d.DepartmentId, d.DepartmentName, d.Specialty, d.DiplomaNo, d.Phone, d.IsActive, services);
}
