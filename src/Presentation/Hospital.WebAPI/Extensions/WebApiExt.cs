using Hospital.Application.Features.Auth;
using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;
using Hospital.WebAPI.Common;
using Scalar.AspNetCore;

namespace Hospital.WebAPI.Extensions;

/// <summary>
/// WebAPI (Presentation) katmanının DI kaydı ve istek hattı.
/// Program.cs'te <c>builder.Services.AddWebApi(builder.Configuration)</c> ve <c>app.UseWebApi()</c> ile çağrılır.
/// </summary>
public static class WebApiExt
{
    public static IServiceCollection AddWebApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpoints(typeof(Program).Assembly);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Ayarlar çalışma anında okunur (testler yapılandırmayı geçersiz kılabilsin diye).
        services.AddSingleton(serviceProvider =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var auth = config.GetSection("Auth");
            var jwt = config.GetSection("Jwt");
            var defaults = AuthSettings.Default;

            return new AuthSettings(
                auth.GetValue("MaxFailedLoginAttempts", defaults.MaxFailedLoginAttempts),
                TimeSpan.FromMinutes(auth.GetValue("LockoutMinutes", defaults.LockoutDuration.TotalMinutes)),
                TimeSpan.FromMinutes(jwt.GetValue("AccessTokenMinutes", defaults.AccessTokenLifetime.TotalMinutes)),
                TimeSpan.FromDays(jwt.GetValue("RefreshTokenDays", defaults.RefreshTokenLifetime.TotalDays)));
        });

        // İmzalama anahtarı eksik/kısa ise uygulama başlarken hata verir (güvensiz varsayılanla çalışmaz).
        services.AddSingleton(serviceProvider =>
        {
            var jwt = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Jwt");
            return new JwtSettings(jwt["Issuer"] ?? string.Empty, jwt["Audience"] ?? string.Empty, jwt["SigningKey"] ?? string.Empty)
                .Validated();
        });

        services.AddJwtAuthentication();
        services.AddFrontendCors(configuration);
        services.AddAuthRateLimiting(configuration);

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        services.AddOpenApiWithBearer();

        return services;
    }

    /// <summary>İstek hattı (middleware) sırası ve endpoint eşlemeleri.</summary>
    public static WebApplication UseWebApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseCors(AuthenticationExt.CorsPolicyName);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }
}
