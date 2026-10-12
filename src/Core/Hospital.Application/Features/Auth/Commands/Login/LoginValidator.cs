using FluentValidation;

namespace Hospital.Application.Features.Auth.Commands.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.").MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Parola zorunludur.").MaximumLength(128);
    }
}
