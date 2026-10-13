using FluentValidation;

namespace Hospital.Application.Features.Hospitals.Commands.SetWorkingHours;

public sealed class SetWorkingHoursValidator : AbstractValidator<SetWorkingHoursCommand>
{
    public SetWorkingHoursValidator()
    {
        RuleFor(x => x.HospitalId).NotEmpty();

        RuleFor(x => x.Hours)
            .NotNull().WithMessage("Çalışma saatleri listesi zorunludur.")
            .Must(hours => hours is null || hours.Select(h => h.Day).Distinct().Count() == hours.Count)
            .WithMessage("Aynı güne birden fazla çalışma aralığı verilemez.");

        RuleForEach(x => x.Hours).ChildRules(hour =>
        {
            hour.RuleFor(h => h.Day).IsInEnum().WithMessage("Geçersiz gün.");
            hour.RuleFor(h => h.CloseTime)
                .GreaterThan(h => h.OpenTime)
                .WithMessage("Kapanış saati açılış saatinden sonra olmalıdır.");
        });
    }
}
