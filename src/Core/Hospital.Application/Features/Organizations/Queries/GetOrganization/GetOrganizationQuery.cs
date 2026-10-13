using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Organizations.Queries.GetOrganization;

/// <summary>Oturum açmış süper yöneticinin kurumunu döner.</summary>
public sealed record GetOrganizationQuery : IQuery<Result<OrganizationDto>>;
