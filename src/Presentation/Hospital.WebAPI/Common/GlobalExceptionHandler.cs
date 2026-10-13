using Hospital.Application.Exceptions;
using Hospital.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hospital.WebAPI.Common;

/// <summary>
/// Tüm istisnaları tek noktada tutarlı ProblemDetails'e çevirir:
/// <list type="bullet">
/// <item>İstek gövdesi/parametre bağlama hataları → 400 + alan bazlı <c>errors</c> (<see cref="RequestErrorTranslator"/>).</item>
/// <item><see cref="DomainException"/> → 422 (iş kuralı ihlali, mesaj istemciye gösterilir).</item>
/// <item><see cref="DataConflictException"/> → 409 (eşzamanlı veri çakışması).</item>
/// <item>Diğerleri → 500, genel mesaj + <c>traceId</c>; ayrıntı yalnız loglanır (Development'ta yanıta da eklenir).</item>
/// </list>
/// </summary>
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetails,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // İstemci isteği iptal ettiyse hata değil; ASP.NET Core kendi akışını sürdürsün.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var problem = exception switch
        {
            BadHttpRequestException bad => FromRequestError(RequestErrorTranslator.Translate(bad)),
            DomainException => Create(StatusCodes.Status422UnprocessableEntity, "İş kuralı ihlali", ProblemCodes.DomainRule, exception.Message),
            DataConflictException => Create(
                StatusCodes.Status409Conflict,
                "Veri çakışması",
                ProblemCodes.DataConflict,
                "İşlem başka bir istekle çakıştı (ör. aynı kayıt aynı anda oluşturuldu). Lütfen tekrar deneyin."),
            _ => Create(
                StatusCodes.Status500InternalServerError,
                "Beklenmeyen bir hata oluştu",
                ProblemCodes.ServerError,
                "Sunucuda beklenmeyen bir hata oluştu. Sorun sürerse traceId değeriyle bildirin."),
        };

        Log(httpContext, exception, problem.Status ?? StatusCodes.Status500InternalServerError);

        if (environment.IsDevelopment() && problem.Status >= StatusCodes.Status500InternalServerError)
        {
            // Geliştirmede neyin patladığı görünsün; üretimde sızdırılmaz.
            problem.Extensions["exceptionType"] = exception.GetType().FullName;
            problem.Extensions["exceptionMessage"] = exception.Message;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails Create(int status, string title, string code, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static ProblemDetails FromRequestError(TranslatedRequestError error)
    {
        ProblemDetails problem = error.Errors is null
            ? new ProblemDetails()
            : new HttpValidationProblemDetails(error.Errors.ToDictionary(pair => pair.Key, pair => pair.Value));

        problem.Status = error.Status;
        problem.Title = error.Title;
        problem.Detail = error.Detail;
        problem.Extensions["code"] = error.Code;
        return problem;
    }

    private void Log(HttpContext httpContext, Exception exception, int status)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "İşlenmeyen hata: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            // İstemci hataları (400/409/422) beklenen akıştır; yığın izi gürültü olur.
            logger.LogWarning("{ExceptionType} → {Status} {Method} {Path}: {Message}",
                exception.GetType().Name, status, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }
    }
}

/// <summary>Tüm hata yanıtlarında bulunan makine tarafından okunabilir <c>code</c> değerleri.</summary>
public static class ProblemCodes
{
    public const string DomainRule = "domain.rule_violation";
    public const string DataConflict = "data.conflict";
    public const string ServerError = "server.error";
    public const string Unauthorized = "auth.unauthorized";
    public const string Forbidden = "auth.forbidden";
    public const string NotFound = "request.not_found";
    public const string MethodNotAllowed = "request.method_not_allowed";
    public const string UnsupportedMediaType = "request.unsupported_media_type";
    public const string RateLimited = "rate_limit.exceeded";
    public const string Generic = "request.error";
}
