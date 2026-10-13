using FluentValidation;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Services.Validation;

/// <summary>Services feature'ının komutları arasında paylaşılan alan kuralları.</summary>
internal static class ServiceRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Hizmet adı zorunludur.").MaximumLength(Service.NameMaxLength);

    public static IRuleBuilderOptions<T, int> MustBeValidDuration<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, Service.MaxDurationMinutes)
            .WithMessage($"Hizmet süresi 1 ile {Service.MaxDurationMinutes} dakika arasında olmalıdır.");

    public static IRuleBuilderOptions<T, decimal> MustBeValidPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThanOrEqualTo(0).WithMessage("Ücret 0'dan küçük olamaz.")
            .LessThanOrEqualTo(Service.MaxPrice).WithMessage("Ücret çok büyük.")
            .PrecisionScale(12, 2, ignoreTrailingZeros: true).WithMessage("Ücret en fazla 2 ondalık basamak içerebilir.");

    public static IRuleBuilderOptions<T, string> MustBeValidCurrency<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Para birimi zorunludur.")
            .Length(3).WithMessage("Para birimi 3 harfli ISO kodu olmalıdır (ör. TRY).")
            .Must(c => c is not null && c.All(char.IsAsciiLetter)).WithMessage("Para birimi yalnızca harf içermelidir.");
}
