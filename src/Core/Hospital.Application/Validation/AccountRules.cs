using FluentValidation;

namespace Hospital.Application.Validation;

/// <summary>Hesap oluşturan komutlar (hasta kaydı, yönetici ile doktor/personel/yönetici oluşturma) arasında paylaşılan alan kuralları.</summary>
internal static class AccountRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("E-posta zorunludur.")
            .MaximumLength(254).WithMessage("E-posta en fazla 254 karakter olabilir.")
            .EmailAddress().WithMessage("E-posta biçimi geçersiz.");

    public static IRuleBuilderOptions<T, string> MustBeValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Parola zorunludur.")
            .MinimumLength(8).WithMessage("Parola en az 8 karakter olmalıdır.")
            .MaximumLength(128).WithMessage("Parola en fazla 128 karakter olabilir.")
            .Must(p => p is not null && p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("Parola en az bir harf ve bir rakam içermelidir.");

    public static IRuleBuilderOptions<T, string> MustBeValidPersonName<T>(this IRuleBuilder<T, string> rule, string label, int maxLength = 100) =>
        rule.NotEmpty().WithMessage($"{label} zorunludur.")
            .MaximumLength(maxLength).WithMessage($"{label} en fazla {maxLength} karakter olabilir.");

    public static IRuleBuilderOptions<T, string?> MustBeValidOptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(32).WithMessage("Telefon en fazla 32 karakter olabilir.");
}
