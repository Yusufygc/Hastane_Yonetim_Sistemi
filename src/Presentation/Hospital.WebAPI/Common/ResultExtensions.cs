using Hospital.Application.Parameters;
using Hospital.Application.Wrappers;

namespace Hospital.WebAPI.Common;

public static class ResultExtensions
{
    /// <summary>Başarısız sonucu ProblemDetails'e çevirir. Başarılı sonuç için çağıran taraf kendi yanıtını üretir.</summary>
    public static IResult ToProblem(this Result result)
    {
        var error = result.Error ?? throw new InvalidOperationException("Başarılı sonuç problem yanıtına çevrilemez.");

        if (error.Type == ErrorType.Validation && error.Details is not null)
        {
            return Results.ValidationProblem(
                error.Details.ToDictionary(pair => pair.Key, pair => pair.Value),
                title: error.Message,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            statusCode: status,
            title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    /// <summary>Başarıda 204 No Content, hatada ProblemDetails döner.</summary>
    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.ToProblem();

    /// <summary>
    /// Sayfalı liste: gövde yalnız kayıtların dizisidir; toplam ve sayfa bilgisi <c>X-Total-Count</c>, <c>X-Page</c>,
    /// <c>X-Page-Size</c> yanıt başlıklarında döner (gövde biçimi sayfalamadan bağımsız kalır).
    /// </summary>
    public static IResult MatchPaged<T>(this Result<PagedList<T>> result)
    {
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var paged = result.Value;
        return new PagedResult<T>(paged);
    }

    /// <summary>Başarıda <paramref name="onSuccess"/>, hatada ProblemDetails döner.</summary>
    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.ToProblem();
}

/// <summary>Kayıt dizisini 200 olarak yazar ve sayfalama başlıklarını ekler.</summary>
internal sealed class PagedResult<T>(PagedList<T> paged) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var headers = httpContext.Response.Headers;
        headers["X-Total-Count"] = paged.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        headers["X-Page"] = paged.Page.Page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        headers["X-Page-Size"] = paged.Page.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return Results.Ok(paged.Items).ExecuteAsync(httpContext);
    }
}
