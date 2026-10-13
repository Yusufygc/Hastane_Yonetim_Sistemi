using Hospital.Application.Features.Services;
using Hospital.Application.Security;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Services;

internal sealed class ServiceReader(AppDbContext db) : IServiceReader
{
    public async Task<IReadOnlyList<ServiceDto>> ListAsync(AdminScope scope, Guid? hospitalId, CancellationToken cancellationToken) =>
        await InScope(scope)
            .Where(s => hospitalId == null || s.HospitalId == hospitalId)
            .OrderBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync(cancellationToken);

    public async Task<ServiceDto?> GetAsync(Guid id, AdminScope scope, CancellationToken cancellationToken) =>
        await InScope(scope)
            .Where(s => s.Id == id)
            .Select(s => ToDto(s))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Yalnızca yöneticinin kapsamındaki şubelerin hizmetleri.</summary>
    private IQueryable<Hospital.Domain.Entities.Facility.Service> InScope(AdminScope scope) =>
        from s in db.Services.AsNoTracking()
        join h in db.Hospitals on s.HospitalId equals h.Id
        where h.OrganizationId == scope.OrganizationId && (scope.HospitalId == null || h.Id == scope.HospitalId)
        select s;

    private static ServiceDto ToDto(Hospital.Domain.Entities.Facility.Service s) =>
        new(s.Id, s.HospitalId, s.DepartmentId, s.Name, s.DurationMinutes, s.Price, s.Currency, s.IsActive);
}
