using FluentValidation;
using Hospital.Application.Features.Doctors.Validation;

namespace Hospital.Application.Features.Doctors.Commands.UpdateMyAvailability;

public sealed class UpdateMyAvailabilityValidator : AbstractValidator<UpdateMyAvailabilityCommand>
{
    public UpdateMyAvailabilityValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Day).MustBeValidDay();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        RuleFor(x => x.ValidFrom).MustBeValidValidFrom();
        RuleFor(x => x.ValidTo)
            .GreaterThanOrEqualTo(x => x.ValidFrom).WithMessage("Geçerlilik bitişi başlangıçtan önce olamaz.")
            .When(x => x.ValidTo is not null);
    }
}
