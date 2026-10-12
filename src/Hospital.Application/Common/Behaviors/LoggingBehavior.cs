using Mediator;
using Microsoft.Extensions.Logging;

namespace Hospital.Application.Common.Behaviors;

/// <summary>Mesaj adını, süresini ve (varsa) hata kodunu loglar. Mesaj içeriği loglanmaz (parola, TC vb.).</summary>
public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : Result
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TMessage).Name;
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        var response = await next(message, cancellationToken);

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt);

        if (response.IsFailure)
        {
            logger.LogInformation("{Message} başarısız ({ErrorCode}) {ElapsedMs} ms", name, response.Error!.Code, elapsed.TotalMilliseconds);
        }
        else
        {
            logger.LogInformation("{Message} tamamlandı {ElapsedMs} ms", name, elapsed.TotalMilliseconds);
        }

        return response;
    }
}
