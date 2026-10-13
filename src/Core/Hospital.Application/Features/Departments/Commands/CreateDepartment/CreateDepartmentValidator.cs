using FluentValidation;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Departments.Commands.CreateDepartment;

public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator()
    {
        RuleFor(x => x.HospitalId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Bölüm adı zorunludur.").MaximumLength(Department.NameMaxLength);
    }
}
