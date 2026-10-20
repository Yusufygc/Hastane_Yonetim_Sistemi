using FluentValidation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Auth.Commands.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mevcut parola zorunludur.");
        RuleFor(x => x.NewPassword).MustBeValidPassword();
    }
}
