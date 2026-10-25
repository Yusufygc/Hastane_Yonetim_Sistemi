using Hospital.Application.Features.Appointments;
using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Notifications;
using Hospital.Domain.Entities.Notifications;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
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

        // Bekleyen randevu talebi zaman aşımı (Appointment:PendingExpiry).
        services.AddSingleton(serviceProvider =>
        {
            var section = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Appointment:PendingExpiry");
            var defaults = PendingExpirySettings.Default;

            return new PendingExpirySettings(
                section.GetValue("Enabled", defaults.Enabled),
                TimeSpan.FromHours(section.GetValue("Hours", defaults.After.TotalHours)),
                TimeSpan.FromSeconds(section.GetValue("PollSeconds", defaults.PollInterval.TotalSeconds)),
                section.GetValue("BatchSize", defaults.BatchSize));
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

        // Hassas veri şifreleme anahtarı (KVKK). Eksik/hatalıysa uygulama başlarken hata verir (ValidateRequiredSettings).
        services.AddSingleton(serviceProvider =>
            new DataProtectionSettings(
                serviceProvider.GetRequiredService<IConfiguration>().GetSection("Security:DataProtection")["Key"] ?? string.Empty));

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

    /// <summary>
    /// Zorunlu gizli ayarları (JWT imza anahtarı, veri şifreleme anahtarı) uygulama ayağa kalkarken doğrular: eksik veya hatalıysa
    /// ilk istekte değil, başlangıçta anlaşılır bir hatayla durur (güvensiz varsayılanla çalışmaz).
    /// </summary>
    public static WebApplication ValidateRequiredSettings(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<JwtSettings>();
        _ = app.Services.GetRequiredService<ISensitiveDataProtector>();
        return app;
    }

    /// <summary>İstek hattı (middleware) sırası ve endpoint eşlemeleri.</summary>
    public static WebApplication UseWebApi(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>(); // en dışta: hata yanıtları ve istek özeti de aynı kimliği taşır
        app.UseExceptionHandler();
        app.UseStatusCodePages(); // gövdesiz hata durumlarını ProblemDetails gövdesine çevirir
        app.UseCors(AuthenticationExt.CorsPolicyName);
        app.UseMiddleware<JsonContentTypeMiddleware>(); // yanlış Content-Type → açık 415 (401'e dönüşmesin)
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapEndpoints();

        // OpenAPI belgesi ve Scalar arayüzü: Development'ta açık; diğer ortamlarda yalnız OpenApi:Enabled=true ise.
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }
}
