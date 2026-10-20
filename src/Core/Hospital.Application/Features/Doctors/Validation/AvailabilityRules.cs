using FluentValidation;

namespace Hospital.Application.Features.Doctors.Validation;

/// <summary>Uygunluk ekleme/güncelleme komutları arasında paylaşılan alan kuralları.</summary>
internal static class AvailabilityRules
{
    public static IRuleBuilderOptions<T, DayOfWeek> MustBeValidDay<T>(this IRuleBuilder<T, DayOfWeek> rule) =>
        rule.IsInEnum().WithMessage("Geçersiz gün (0 = Pazar … 6 = Cumartesi).");

    public static IRuleBuilderOptions<T, DateOnly> MustBeValidValidFrom<T>(this IRuleBuilder<T, DateOnly> rule) =>
        rule.Must(date => date != default).WithMessage("Geçerlilik başlangıcı zorunludur (yyyy-MM-dd).");
}
