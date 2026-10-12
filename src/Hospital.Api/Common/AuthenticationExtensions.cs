using System.Security.Claims;
using System.Threading.RateLimiting;
using Hospital.Application.Common.Security;
using Hospital.Application.Features.Auth;
using Hospital.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Hospital.Api.Common;

public static class AuthenticationExtensions
{
    public const string CookieName = "hospital.auth";
    public const string CsrfCookieName = "hospital.csrf";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";
    public const string AuthRateLimitPolicy = "auth";
    public const string CorsPolicyName = "Frontend";

    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                // API yönlendirme yapmaz: kimlik yok → 401, rol yetersiz → 403.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                // Her istekte hesap aktif mi ve security stamp hâlâ aynı mı? (pasife alma / parola değişimi → oturum düşer)
                options.Events.OnValidatePrincipal = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var stamp = context.Principal?.FindFirstValue(SecurityStampClaim);

                    var valid = Guid.TryParse(userId, out var id)
                                && !string.IsNullOrEmpty(stamp)
                                && await context.HttpContext.RequestServices
                                    .GetRequiredService<IAccountSessionReader>()
                                    .IsSessionValidAsync(id, stamp, context.HttpContext.RequestAborted);

                    if (!valid)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
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
                .RequireClaim(ScopeClaimTypes.AccessLevel, "SUPER_ADMIN"));

        return services;
    }

    public const string SecurityStampClaim = "security_stamp";

    public static ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(SecurityStampClaim, user.SecurityStamp),
        };

        claims.AddRange(user.ScopeClaims.Select(pair => new Claim(pair.Key, pair.Value)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static IServiceCollection AddCsrfProtection(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeaderName;
            options.Cookie.Name = CsrfCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        return services;
    }

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            // Çerez gönderimi (AllowCredentials) için joker kaynak kullanılamaz; kaynaklar açıkça listelenir.
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            policy
                .WithOrigins(origins)
                .AllowCredentials()
                .WithHeaders("Content-Type", CsrfHeaderName)
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE");
        }));

        return services;
    }

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

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
