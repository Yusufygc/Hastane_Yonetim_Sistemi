using FluentValidation;

namespace Hospital.Application.Features.Hospitals.Validation;

/// <summary>Hospitals feature'ına ait ortak doğrulama kuralları (aynı feature içinde paylaşılır).</summary>
internal static class HospitalRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidTimeZone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Saat dilimi zorunludur.")
            .Must(IsKnownTimeZone).WithMessage("Geçersiz saat dilimi (IANA kimliği bekleniyor, ör. Europe/Istanbul).");

    private static bool IsKnownTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
