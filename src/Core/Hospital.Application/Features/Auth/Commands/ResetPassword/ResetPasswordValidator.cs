using FluentValidation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Auth.Commands.ResetPassword;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Sıfırlama belirteci zorunludur.").MaximumLength(256).WithMessage("Sıfırlama belirteci geçersiz.");
        RuleFor(x => x.NewPassword).MustBeValidPassword();
    }
}
