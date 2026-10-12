using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Hospital.WebAPI.Common;

/// <summary>
/// Cookie kimlik doğrulaması tarayıcı tarafından otomatik gönderildiği için /api altındaki durum değiştiren
/// istekler (POST/PUT/PATCH/DELETE) geçerli bir antiforgery belirteci (X-XSRF-TOKEN başlığı) ister.
/// Belirteç GET /api/auth/csrf ile alınır. Kimlik değiştiğinde (giriş/çıkış) belirteç yenilenmelidir.
/// </summary>
internal sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (RequiresValidation(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Geçersiz veya eksik CSRF belirteci.",
                        Extensions = { ["code"] = "csrf.invalid" },
                    },
                    context.RequestAborted);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        && (HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method)
            || HttpMethods.IsDelete(request.Method));
}
