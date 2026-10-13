using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.WebAPI.Extensions;

public static class DatabaseMigrationExt
{
    /// <summary>`Database:MigrateOnStartup` açıksa bekleyen migration'ları uygular (geliştirme/test için).</summary>
    public static async Task MigrateDatabaseIfConfiguredAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>`Database:SeedOnStartup` açıksa varsayılan kurum/şube ve ilk süper yöneticiyi (idempotent) oluşturur.</summary>
    public static async Task SeedDatabaseIfConfiguredAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();
    }
}
