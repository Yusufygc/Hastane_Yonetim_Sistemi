using System.Text;
using System.Threading.RateLimiting;
using Hospital.Application.Features.Auth;
using Hospital.Application.Security;
using Hospital.Domain.Entities.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Hospital.WebAPI.Extensions;

public static class AuthenticationExt
{
    public const string AuthRateLimitPolicy = "auth";
    public const string CorsPolicyName = "Frontend";

    /// <summary>
    /// JWT Bearer kimlik doğrulaması + rol politikaları. Belirteç doğrulama ayarları çalışma anında
    /// <see cref="JwtSettings"/>'ten okunur (testler yapılandırmayı geçersiz kılabilsin diye).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((options, jwt) =>
            {
                // Claim adları olduğu gibi kalır (sub, role, ...); eski WS-* eşlemesi kapalı.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Subject,
                    RoleClaimType = ClaimNames.Role,
                };

                options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionAsync };
            });

        services.AddAuthorizationBuilder()
            // Varsayılan kapalı: politikası/AllowAnonymous'u olmayan uç anonim erişime açık kalmaz.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Authenticated, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Policies.Patient, policy => policy.RequireRole(RoleNames.Patient))
            .AddPolicy(Policies.Doctor, policy => policy.RequireRole(RoleNames.Doctor))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(RoleNames.Admin))
            .AddPolicy(Policies.SuperAdmin, policy => policy
                .RequireRole(RoleNames.Admin)
                .RequireClaim(ScopeClaimTypes.AccessLevel, AdminAccessLevelNames.SuperAdmin));

        return services;
    }

    /// <summary>
    /// Her istekte hesap hâlâ aktif mi ve güvenlik damgası (security_stamp) aynı mı? Pasife alınan veya parolası
    /// değişen kullanıcının henüz süresi dolmamış erişim belirteci de anında geçersiz olur (JWT'nin iptal edilememe sorununu kapatır).
    /// </summary>
    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var userId = principal?.FindFirst(ClaimNames.Subject)?.Value;
        var stamp = principal?.FindFirst(ClaimNames.SecurityStamp)?.Value;

        var valid = Guid.TryParse(userId, out var id)
                    && !string.IsNullOrEmpty(stamp)
                    && await context.HttpContext.RequestServices
                        .GetRequiredService<IAccountSessionReader>()
                        .IsSessionValidAsync(id, stamp, context.HttpContext.RequestAborted);

        if (!valid)
        {
            context.Fail("Oturum geçersiz.");
        }
    }

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            // Bearer belirteci Authorization başlığında taşınır; çerez olmadığı için AllowCredentials gerekmez.
            // Yine de kaynaklar açıkça listelenir, joker kullanılmaz.
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            policy
                .WithOrigins(origins)
                .WithHeaders("Content-Type", "Authorization")
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE");
        }));

        return services;
    }

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 429 yanıtı da ProblemDetails gövdesi + Retry-After başlığı taşır.
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                string? retryAfterText = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    retryAfterText = $" {Math.Ceiling(retryAfter.TotalSeconds)} saniye sonra tekrar deneyin.";
                }

                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = $"Giriş/kayıt/yenileme uçlarına çok fazla istek gönderildi.{retryAfterText}",
                };

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                    new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
            };

            // Yapılandırma istek anında okunur (testler değerleri geçersiz kılabilsin diye).
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
            {
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = config.GetValue("RateLimiting:Auth:PermitLimit", 10);
                var windowSeconds = config.GetValue("RateLimiting:Auth:WindowSeconds", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }
}
