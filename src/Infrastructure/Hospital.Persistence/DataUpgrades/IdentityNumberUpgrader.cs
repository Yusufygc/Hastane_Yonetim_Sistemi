using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hospital.Persistence.DataUpgrades;

/// <summary>
/// Şifrelemeden önce yazılmış (kör indeksi boş) hasta kayıtlarının T.C. kimlik numaralarını şifreler ve kör indeksini yazar.
/// Idempotenttir: yükseltilmiş kayıt bir daha seçilmez. <c>Database:MigrateOnStartup</c> açıkken migration'dan hemen sonra çalışır;
/// aksi halde dağıtım adımı olarak çağrılmalıdır. Şifreleme ve indeks <see cref="AppDbContext"/> içinde otomatik uygulanır.
/// </summary>
public sealed class IdentityNumberUpgrader(AppDbContext db, ILogger<IdentityNumberUpgrader> logger)
{
    private const int BatchSize = 200;

    public async Task<int> UpgradeAsync(CancellationToken cancellationToken = default)
    {
        var total = 0;

        while (true)
        {
            var batch = await db.Patients
                .Where(p => EF.Property<string?>(p, AppDbContext.PatientIdentityHash) == null)
                .OrderBy(p => p.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            // Değer okunurken (eski düz metin) çözülmüş gelir; değişmemiş görünse de yeniden yazılması (şifrelenmesi) zorlanır.
            foreach (var patient in batch)
            {
                db.Entry(patient).Property(p => p.IdentityNumber).IsModified = true;
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            total += batch.Count;
        }

        if (total > 0)
        {
            logger.LogInformation("{Count} hastanın T.C. kimlik numarası şifrelendi (KVKK yükseltmesi).", total);
        }

        return total;
    }
}
