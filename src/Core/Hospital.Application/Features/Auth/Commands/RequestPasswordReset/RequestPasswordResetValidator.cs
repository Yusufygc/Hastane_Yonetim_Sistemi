using FluentValidation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Auth.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(x => x.Email).MustBeValidEmail();
    }
}
