using FluentValidation;
using Hospital.Application.Features.Services.Validation;

namespace Hospital.Application.Features.Services.Commands.UpdateService;

public sealed class UpdateServiceValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MustBeValidName();
        RuleFor(x => x.DurationMinutes).MustBeValidDuration();
        RuleFor(x => x.Price).MustBeValidPrice();
        RuleFor(x => x.Currency).MustBeValidCurrency();
    }
}
