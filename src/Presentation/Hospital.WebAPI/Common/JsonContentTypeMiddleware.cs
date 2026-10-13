using Microsoft.AspNetCore.Mvc;

namespace Hospital.WebAPI.Common;

/// <summary>
/// /api altında gövde taşıyan isteklerin <c>Content-Type: application/json</c> olmasını yetkilendirmeden ÖNCE denetler.
/// Neden: ASP.NET Core yönlendirmesi içerik türü uymayan isteği yapay bir "415" uç noktasına yönlendirir; bu uçta
/// AllowAnonymous olmadığı için varsayılan-kapalı politika devreye girer ve anonim istemci yanlış içerik türü için
/// "401 giriş gerekir" görür (kafa karıştırıcı). Bu ara katman gerçek nedeni açıkça 415 olarak bildirir.
/// </summary>
internal sealed class JsonContentTypeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var request = context.Request;

        if (request.Path.StartsWithSegments("/api") && HasBody(request) && !request.HasJsonContentType())
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status415UnsupportedMediaType,
                    Detail = $"İstek gövdesi `Content-Type: application/json` ile gönderilmelidir (gönderilen: {request.ContentType ?? "yok"}).",
                },
            });

            return;
        }

        await next(context);
    }

    private static bool HasBody(HttpRequest request) =>
        request.ContentLength is > 0 || request.Headers.TransferEncoding.Count > 0;
}
