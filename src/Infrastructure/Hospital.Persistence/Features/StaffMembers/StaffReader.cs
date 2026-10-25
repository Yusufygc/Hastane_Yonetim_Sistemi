using Hospital.Application.Features.StaffMembers;
using Hospital.Application.Parameters;
using Hospital.Application.Security;
using Hospital.Persistence.Context;
using Hospital.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.StaffMembers;

internal sealed class StaffReader(AppDbContext db) : IStaffReader
{
    public async Task<PagedList<StaffDto>> ListAsync(AdminScope scope, Guid? hospitalId, PageRequest page, CancellationToken cancellationToken) =>
        await (
                from s in db.StaffMembers.AsNoTracking()
                join h in db.Hospitals on s.HospitalId equals h.Id
                where h.OrganizationId == scope.OrganizationId
                      && (scope.HospitalId == null || h.Id == scope.HospitalId)
                      && (hospitalId == null || h.Id == hospitalId)
                orderby s.LastName, s.FirstName, s.Id
                select new StaffDto(s.Id, s.Email, s.FirstName, s.LastName, s.Title, s.HospitalId, s.DepartmentId, s.IsActive))
            .ToPagedListAsync(page, cancellationToken);
}
