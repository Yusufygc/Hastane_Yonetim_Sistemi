using Hospital.Application.Wrappers;
using FluentValidation;
using Mediator;

namespace Hospital.Application.Behaviors;

/// <summary>
/// Command/Query çalışmadan önce FluentValidation validator'larını çalıştırır; hata varsa
/// handler'a girmeden doğrulama hatası döner. Tüm mesajlar Result/Result&lt;T&gt; döner.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : Result, IFailureFactory<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validatorList)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        // Anahtarlar JSON'daki alan adlarıyla aynı (camelCase) döner: "BirthDate" → "birthDate", "Hours[0].CloseTime" → "hours[0].closeTime".
        var details = failures
            .GroupBy(f => FieldNaming.ToCamelCasePath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        return TResponse.Failure(Error.Validation("validation.failed", "Bir veya daha fazla alan geçersiz.", details));
    }
}
