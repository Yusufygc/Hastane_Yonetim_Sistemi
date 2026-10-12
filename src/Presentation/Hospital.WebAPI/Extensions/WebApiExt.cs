using Hospital.Application.Features.Auth;
using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;
using Hospital.WebAPI.Common;
using Scalar.AspNetCore;

namespace Hospital.WebAPI.Extensions;

/// <summary>
/// WebAPI (Presentation) katmanının DI kaydı ve istek hattı.
/// Program.cs'te <c>builder.Services.AddWebApi(...)</c> ve <c>app.UseWebApi()</c> ile çağrılır.
/// </summary>
public static class WebApiExt
{
    public static IServiceCollection AddWebApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddEndpoints(typeof(Program).Assembly);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton(serviceProvider =>
        {
            var section = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Auth");
            return new AuthSettings(
                section.GetValue("MaxFailedLoginAttempts", AuthSettings.Default.MaxFailedLoginAttempts),
                TimeSpan.FromMinutes(section.GetValue("LockoutMinutes", AuthSettings.Default.LockoutDuration.TotalMinutes)));
        });

        services.AddCookieAuthentication(environment);
        services.AddCsrfProtection();
        services.AddFrontendCors(configuration);
        services.AddAuthRateLimiting(configuration);

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        services.AddOpenApi();

        return services;
    }

    /// <summary>İstek hattı (middleware) sırası ve endpoint eşlemeleri.</summary>
    public static WebApplication UseWebApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseCors(AuthenticationExt.CorsPolicyName);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<CsrfProtectionMiddleware>();
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
