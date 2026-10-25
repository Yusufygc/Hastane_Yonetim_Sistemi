using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Hospital.WebAPI.Common;

/// <summary>
/// İstek izleme: her isteğe bir <c>X-Correlation-Id</c> atanır (istemci geçerli bir değer gönderirse o korunur), yanıta
/// geri yazılır, <see cref="HttpContext.TraceIdentifier"/> yapılır (hata yanıtlarındaki <c>traceId</c> aynı değerdir) ve
/// log kapsamına konur; böylece bir kullanıcı şikâyeti tek kimlikle loglarda bulunur. İstek bitince tek satırlık özet
/// loglanır: metot, <b>rota şablonu</b>, durum, süre. Sorgu dizesi, gövde, başlıklar ve gerçek yol (kimlikler) loglanmaz.
/// </summary>
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ValidId();

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = ValidId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();
                LogRequest(context, stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private void LogRequest(HttpContext context, long elapsedMilliseconds)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(eşleşmeyen rota)";
        var level = route == "/health" ? LogLevel.Debug : LogLevel.Information;

        logger.Log(
            level,
            "HTTP {Method} {Route} -> {StatusCode} ({ElapsedMs} ms)",
            context.Request.Method,
            route,
            context.Response.StatusCode,
            elapsedMilliseconds);
    }
}
