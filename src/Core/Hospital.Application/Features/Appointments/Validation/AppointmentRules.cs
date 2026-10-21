using FluentValidation;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Appointments.Validation;

/// <summary>Randevu sorgularında paylaşılan filtre doğrulamaları.</summary>
internal static class AppointmentRules
{
    /// <summary>Boş durum filtresi geçerlidir (tümü); doluysa bilinen bir durum olmalıdır.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeValidStatusFilter<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(status => string.IsNullOrWhiteSpace(status) || AppointmentStatusNames.TryParse(status, out _))
            .WithMessage("Durum PENDING, APPROVED, REJECTED, CANCELLED veya COMPLETED olmalıdır.");

    public static IRuleBuilderOptions<T, DateTimeOffset?> MustNotPrecede<T>(
        this IRuleBuilder<T, DateTimeOffset?> rule,
        Func<T, DateTimeOffset?> from) =>
        rule.Must((instance, to) => to is null || from(instance) is null || to >= from(instance))
            .WithMessage("Bitiş (to) başlangıçtan (from) önce olamaz.");
}
