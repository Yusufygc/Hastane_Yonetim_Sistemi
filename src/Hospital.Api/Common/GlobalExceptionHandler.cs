using Hospital.Application.Common;
using Hospital.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hospital.Api.Common;

/// <summary>Beklenmeyen ve domain/veri çakışması istisnalarını tek noktada ProblemDetails'e çevirir.</summary>
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "İş kuralı ihlali", exception.Message),
            DataConflictException => (StatusCodes.Status409Conflict, "Veri çakışması", "İşlem başka bir istekle çakıştı. Lütfen tekrar deneyin."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Geçersiz istek", null),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu", null),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "İşlenmeyen hata: {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("{ExceptionType}: {Message}", exception.GetType().Name, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
