using FluentValidation;

namespace Hospital.Application.Features.Doctors.Commands.RequestMyService;

public sealed class RequestMyServiceValidator : AbstractValidator<RequestMyServiceCommand>
{
    public RequestMyServiceValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("Hizmet zorunludur.");
    }
}
