using FluentValidation;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Departments.Commands.UpdateDepartment;

public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Bölüm adı zorunludur.").MaximumLength(Department.NameMaxLength);
    }
}
