using Hospital.Application.Interfaces;
using Hospital.Persistence.Context;
using Hospital.Persistence.DataUpgrades;
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

        // Şifrelemeden önce yazılmış kayıtları (T.C. kimlik no) şifreler; yükseltilmiş kayıt varsa hiçbir şey yapmaz.
        await scope.ServiceProvider.GetRequiredService<IdentityNumberUpgrader>().UpgradeAsync();
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
