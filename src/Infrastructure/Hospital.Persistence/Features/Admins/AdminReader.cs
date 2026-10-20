using Hospital.Application.Features.Admins;
using Hospital.Application.Security;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Admins;

internal sealed class AdminReader(AppDbContext db) : IAdminReader
{
    public async Task<IReadOnlyList<AdminDto>> ListAsync(AdminScope scope, CancellationToken cancellationToken)
    {
        var rows = await db.Admins
            .AsNoTracking()
            .Where(a => a.OrganizationId == scope.OrganizationId
                        && (scope.HospitalId == null || a.HospitalId == scope.HospitalId))
            .OrderBy(a => a.LastName).ThenBy(a => a.FirstName)
            .Select(a => new
            {
                a.Id,
                a.Email,
                a.FirstName,
                a.LastName,
                a.AccessLevel,
                a.OrganizationId,
                a.HospitalId,
                a.IsActive,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(a => new AdminDto(
                a.Id,
                a.Email,
                a.FirstName,
                a.LastName,
                AdminAccessLevelNames.From(a.AccessLevel),
                a.OrganizationId,
                a.HospitalId,
                a.IsActive))
            .ToList();
    }
}
