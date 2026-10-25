using FluentValidation;
using Hospital.Application.Parameters;

namespace Hospital.Application.Validation;

/// <summary>Liste sorgularında paylaşılan sayfalama doğrulaması (<c>page</c> ≥ 1, <c>pageSize</c> 1…200).</summary>
internal static class PagingRules
{
    public static IRuleBuilderOptions<T, int?> MustBeValidPage<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(page => page is null or >= 1).WithMessage("Sayfa numarası (page) 1 veya daha büyük olmalıdır.");

    public static IRuleBuilderOptions<T, int?> MustBeValidPageSize<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(size => size is null or (>= 1 and <= PageRequest.MaxPageSize))
            .WithMessage($"Sayfa boyutu (pageSize) 1 ile {PageRequest.MaxPageSize} arasında olmalıdır.");
}
