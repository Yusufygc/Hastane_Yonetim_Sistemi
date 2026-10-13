using FluentValidation;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Organizations.Commands.UpdateOrganization;

public sealed class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kurum adı zorunludur.").MaximumLength(Organization.NameMaxLength);
}
