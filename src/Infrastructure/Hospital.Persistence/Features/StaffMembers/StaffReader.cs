using Hospital.Application.Features.StaffMembers;
using Hospital.Application.Security;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.StaffMembers;

internal sealed class StaffReader(AppDbContext db) : IStaffReader
{
    public async Task<IReadOnlyList<StaffDto>> ListAsync(AdminScope scope, Guid? hospitalId, CancellationToken cancellationToken) =>
        await (
                from s in db.StaffMembers.AsNoTracking()
                join h in db.Hospitals on s.HospitalId equals h.Id
                where h.OrganizationId == scope.OrganizationId
                      && (scope.HospitalId == null || h.Id == scope.HospitalId)
                      && (hospitalId == null || h.Id == hospitalId)
                orderby s.LastName, s.FirstName
                select new StaffDto(s.Id, s.Email, s.FirstName, s.LastName, s.Title, s.HospitalId, s.DepartmentId, s.IsActive))
            .ToListAsync(cancellationToken);
}
