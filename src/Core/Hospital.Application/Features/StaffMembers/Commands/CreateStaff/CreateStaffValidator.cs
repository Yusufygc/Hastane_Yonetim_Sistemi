using FluentValidation;
using Hospital.Application.Validation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.StaffMembers.Commands.CreateStaff;

public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.HospitalId).NotEmpty().WithMessage("Şube zorunludur.");
        RuleFor(x => x.Email).MustBeValidEmail();
        RuleFor(x => x.Password).MustBeValidPassword();
        RuleFor(x => x.FirstName).MustBeValidPersonName("Ad", Staff.NameMaxLength);
        RuleFor(x => x.LastName).MustBeValidPersonName("Soyad", Staff.NameMaxLength);
        RuleFor(x => x.Title).MustBeValidPersonName("Ünvan", Staff.TitleMaxLength);
    }
}
