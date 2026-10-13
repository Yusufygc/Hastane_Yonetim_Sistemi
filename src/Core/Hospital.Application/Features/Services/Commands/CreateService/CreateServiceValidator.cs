using FluentValidation;
using Hospital.Application.Features.Services.Validation;

namespace Hospital.Application.Features.Services.Commands.CreateService;

public sealed class CreateServiceValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceValidator()
    {
        RuleFor(x => x.HospitalId).NotEmpty();
        RuleFor(x => x.Name).MustBeValidName();
        RuleFor(x => x.DurationMinutes).MustBeValidDuration();
        RuleFor(x => x.Price).MustBeValidPrice();
        RuleFor(x => x.Currency).MustBeValidCurrency();
    }
}
