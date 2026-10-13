using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Organizations.Commands.UpdateOrganization;

public sealed class UpdateOrganizationHandler(
    IOrganizationRepository organizations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateOrganizationCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateOrganizationCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is not { IsSuperAdmin: true })
        {
            return OrganizationErrors.Forbidden;
        }

        var organization = await organizations.GetByIdAsync(scope.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        organization.Rename(command.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
