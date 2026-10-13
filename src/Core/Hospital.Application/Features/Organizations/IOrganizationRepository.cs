using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Organizations;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record OrganizationDto(Guid Id, string Name, bool IsActive);

public static class OrganizationErrors
{
    public static readonly Wrappers.Error Forbidden =
        Wrappers.Error.Forbidden("organization.forbidden", "Bu işlem yalnızca süper yönetici içindir.");

    public static readonly Wrappers.Error NotFound =
        Wrappers.Error.NotFound("organization.not_found", "Kurum bulunamadı.");
}
