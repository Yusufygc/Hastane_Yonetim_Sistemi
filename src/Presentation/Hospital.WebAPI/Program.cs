using Hospital.WebAPI.Common;
using Hospital.Application;
using Hospital.Application.Interfaces;
using Hospital.Application.Features.Auth;
using Hospital.Infrastructure;
using Hospital.Persistence;
using Hospital.Persistence.Context;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddPersistence();
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>().GetSection("Auth");
    return new AuthSettings(
        config.GetValue("MaxFailedLoginAttempts", AuthSettings.Default.MaxFailedLoginAttempts),
        TimeSpan.FromMinutes(config.GetValue("LockoutMinutes", AuthSettings.Default.LockoutDuration.TotalMinutes)));
});

builder.Services.AddCookieAuthentication(builder.Environment);
builder.Services.AddCsrfProtection();
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddAuthRateLimiting(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddOpenApi();

var app = builder.Build();

await app.MigrateDatabaseIfConfiguredAsync();

app.UseExceptionHandler();
app.UseCors(AuthenticationExtensions.CorsPolicyName);
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

app.Run();

// WebApplicationFactory<Program> için.
public partial class Program;
