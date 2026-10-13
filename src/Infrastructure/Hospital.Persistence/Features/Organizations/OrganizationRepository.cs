using Hospital.Application.Features.Organizations;
using Hospital.Domain.Entities.Facility;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Organizations;

internal sealed class OrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}
