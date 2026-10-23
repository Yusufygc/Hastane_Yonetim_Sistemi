using Hospital.Application.Features.Appointments;
using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Notifications;
using Hospital.Domain.Entities.Notifications;
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

        // Randevu kuralları (ızgara, hazırlık süresi, ufuk, iptal süresi) de çalışma anında okunur.
        services.AddSingleton(serviceProvider =>
        {
            var section = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Appointment");
            var defaults = AppointmentSettings.Default;

            return new AppointmentSettings(
                section.GetValue("SlotGridMinutes", defaults.SlotGridMinutes),
                section.GetValue("MinLeadMinutes", defaults.MinLeadMinutes),
                section.GetValue("MaxBookingHorizonDays", defaults.MaxBookingHorizonDays),
                section.GetValue("CancellationDeadlineHours", defaults.CancellationDeadlineHours));
        });

        // E-posta (SMTP) ve outbox işleyici ayarları. Parola yalnız ortam değişkeni/gizli depo ile verilir.
        services.AddSingleton(serviceProvider =>
        {
            var email = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Email");
            var smtp = email.GetSection("Smtp");
            var outbox = email.GetSection("Outbox");
            var defaults = NotificationSettings.Default;

            var security = Enum.TryParse<SmtpSecurity>(smtp["Security"], ignoreCase: true, out var parsed)
                ? parsed
                : defaults.Smtp.Security;

            return new NotificationSettings(
                new SmtpSettings(
                    smtp["Host"] ?? defaults.Smtp.Host,
                    smtp.GetValue("Port", defaults.Smtp.Port),
                    security,
                    smtp["UserName"],
                    smtp["Password"],
                    smtp["FromAddress"] ?? defaults.Smtp.FromAddress,
                    smtp["FromName"] ?? defaults.Smtp.FromName),
                new OutboxSettings(
                    outbox.GetValue("Enabled", defaults.Outbox.Enabled),
                    TimeSpan.FromSeconds(outbox.GetValue("PollSeconds", defaults.Outbox.PollInterval.TotalSeconds)),
                    outbox.GetValue("BatchPerCycle", defaults.Outbox.BatchPerCycle),
                    new RetryPolicy(
                        outbox.GetValue("MaxAttempts", defaults.Outbox.Retry.MaxAttempts),
                        TimeSpan.FromSeconds(outbox.GetValue("BaseDelaySeconds", defaults.Outbox.Retry.BaseDelay.TotalSeconds)),
                        TimeSpan.FromSeconds(outbox.GetValue("MaxDelaySeconds", defaults.Outbox.Retry.MaxDelay.TotalSeconds)))));
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

        // Gövde/parametre bağlama hatalarında (geçersiz JSON, hatalı tarih vb.) istisna fırlatılsın: varsayılan yalnızca
        // Development'ta açıktır; üretimde kapalı olsaydı bu hatalar ayrıntısız, gövdesiz 400 olarak dönerdi.
        // GlobalExceptionHandler bu istisnaları alan bazlı Türkçe hatalara çevirir.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        // Tüm hata yanıtları (gövdesiz 401/403/404/429 dahil) tek biçimde: Türkçe başlık, code, traceId.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomizer.Apply);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        services.AddOpenApiWithBearer();

        return services;
    }

    /// <summary>İstek hattı (middleware) sırası ve endpoint eşlemeleri.</summary>
    public static WebApplication UseWebApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(); // gövdesiz hata durumlarını ProblemDetails gövdesine çevirir
        app.UseCors(AuthenticationExt.CorsPolicyName);
        app.UseMiddleware<JsonContentTypeMiddleware>(); // yanlış Content-Type → açık 415 (401'e dönüşmesin)
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
