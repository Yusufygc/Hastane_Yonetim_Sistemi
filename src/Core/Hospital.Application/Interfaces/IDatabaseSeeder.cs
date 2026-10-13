namespace Hospital.Application.Interfaces;

/// <summary>
/// İlk kurulum verisini (varsayılan kurum/şube, örnek bölüm-hizmet, ilk süper yönetici) idempotent olarak yazar.
/// Migration sonrası, yapılandırma `Database:SeedOnStartup` açıksa çalıştırılır.
/// </summary>
public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
