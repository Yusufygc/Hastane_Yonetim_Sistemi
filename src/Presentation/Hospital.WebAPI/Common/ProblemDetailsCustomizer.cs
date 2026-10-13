namespace Hospital.WebAPI.Common;

/// <summary>
/// Gövdesiz kalan hata yanıtlarını (401, 403, 404, 405, 429 …) Türkçe, makine koduyla ve traceId ile standartlaştırır.
/// Kendi <c>code</c> değerini taşıyan (ör. iş hatası) yanıtlara dokunmaz.
/// </summary>
public static class ProblemDetailsCustomizer
{
    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var httpContext = context.HttpContext;

        problem.Extensions.TryAdd("traceId", httpContext.TraceIdentifier);

        if (problem.Extensions.ContainsKey("code"))
        {
            return; // Bu yanıtı kendi kodu ve mesajıyla biz ürettik.
        }

        var status = problem.Status ?? httpContext.Response.StatusCode;

        var (title, code) = status switch
        {
            StatusCodes.Status400BadRequest => ("Geçersiz istek", ProblemCodes.Generic),
            StatusCodes.Status401Unauthorized => ("Kimlik doğrulaması gerekli", ProblemCodes.Unauthorized),
            StatusCodes.Status403Forbidden => ("Bu işlem için yetkiniz yok", ProblemCodes.Forbidden),
            StatusCodes.Status404NotFound => ("Kaynak bulunamadı", ProblemCodes.NotFound),
            StatusCodes.Status405MethodNotAllowed => ("HTTP metodu desteklenmiyor", ProblemCodes.MethodNotAllowed),
            StatusCodes.Status415UnsupportedMediaType => ("Desteklenmeyen içerik türü", ProblemCodes.UnsupportedMediaType),
            StatusCodes.Status429TooManyRequests => ("Çok fazla istek", ProblemCodes.RateLimited),
            >= 500 => ("Beklenmeyen bir hata oluştu", ProblemCodes.ServerError),
            _ => (problem.Title ?? "Hata", ProblemCodes.Generic),
        };

        problem.Title = title;
        problem.Extensions["code"] = code;

        problem.Detail ??= status switch
        {
            StatusCodes.Status401Unauthorized => DescribeUnauthorized(httpContext),
            StatusCodes.Status403Forbidden => "Hesabınızın rolü bu işlem için yeterli değil.",
            StatusCodes.Status404NotFound => $"'{httpContext.Request.Method} {httpContext.Request.Path}' adresinde bir kaynak yok.",
            StatusCodes.Status405MethodNotAllowed => $"'{httpContext.Request.Path}' adresi {httpContext.Request.Method} metodunu desteklemiyor.",
            StatusCodes.Status415UnsupportedMediaType => "İstek gövdesi `Content-Type: application/json` ile gönderilmelidir.",
            StatusCodes.Status429TooManyRequests => "Kısa sürede çok fazla istek gönderildi. Bir süre bekleyip tekrar deneyin.",
            _ => null,
        };
    }

    /// <summary>
    /// JWT Bearer'ın 401 nedenini (WWW-Authenticate başlığı) insan okuyabilir ipucuna çevirir:
    /// token yok / süresi dolmuş / imza geçersiz / hesap artık geçerli değil.
    /// </summary>
    private static string DescribeUnauthorized(HttpContext httpContext)
    {
        var challenge = httpContext.Response.Headers.WWWAuthenticate.ToString();

        if (string.IsNullOrEmpty(challenge) || !challenge.Contains("error=", StringComparison.OrdinalIgnoreCase))
        {
            var hadAuthorizationHeader = httpContext.Request.Headers.ContainsKey("Authorization");
            return hadAuthorizationHeader
                ? "Authorization başlığı geçerli bir Bearer token içermiyor. Biçim: 'Authorization: Bearer <accessToken>'."
                : "Bu uç için giriş gerekir. 'Authorization: Bearer <accessToken>' başlığını gönderin (token'ı POST /api/auth/login ile alın).";
        }

        if (challenge.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            return "Erişim token'ının süresi dolmuş. POST /api/auth/refresh ile yenileyin veya yeniden giriş yapın.";
        }

        if (challenge.Contains("signature", StringComparison.OrdinalIgnoreCase))
        {
            return "Token imzası geçersiz (token değiştirilmiş veya başka bir anahtarla üretilmiş).";
        }

        if (challenge.Contains("audience", StringComparison.OrdinalIgnoreCase) || challenge.Contains("issuer", StringComparison.OrdinalIgnoreCase))
        {
            return "Token bu API için üretilmemiş (issuer/audience uyuşmuyor).";
        }

        return "Token geçersiz. Yeniden giriş yapın veya POST /api/auth/refresh kullanın.";
    }
}
