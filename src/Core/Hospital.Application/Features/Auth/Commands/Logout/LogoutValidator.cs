using FluentValidation;

namespace Hospital.Application.Features.Auth.Commands.Logout;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Yenileme belirteci zorunludur.").MaximumLength(256);
}
