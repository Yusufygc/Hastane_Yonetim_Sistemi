using FluentValidation;

namespace Hospital.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Yenileme belirteci zorunludur.").MaximumLength(256);
}
