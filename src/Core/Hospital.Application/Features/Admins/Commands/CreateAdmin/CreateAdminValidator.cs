using FluentValidation;
using Hospital.Application.Validation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Admins.Commands.CreateAdmin;

public sealed class CreateAdminValidator : AbstractValidator<CreateAdminCommand>
{
    public CreateAdminValidator()
    {
        RuleFor(x => x.AccessLevel)
            .Must(level => level is AdminAccessLevelNames.SuperAdmin or AdminAccessLevelNames.BranchManager)
            .WithMessage("Erişim seviyesi SUPER_ADMIN veya BRANCH_MANAGER olmalıdır.");

        RuleFor(x => x.HospitalId)
            .NotNull().WithMessage("Şube yöneticisi için şube zorunludur.")
            .When(x => x.AccessLevel == AdminAccessLevelNames.BranchManager);

        RuleFor(x => x.HospitalId)
            .Null().WithMessage("Süper yönetici belirli bir şubeye bağlanamaz.")
            .When(x => x.AccessLevel == AdminAccessLevelNames.SuperAdmin);

        RuleFor(x => x.Email).MustBeValidEmail();
        RuleFor(x => x.Password).MustBeValidPassword();
        RuleFor(x => x.FirstName).MustBeValidPersonName("Ad");
        RuleFor(x => x.LastName).MustBeValidPersonName("Soyad");
    }
}
