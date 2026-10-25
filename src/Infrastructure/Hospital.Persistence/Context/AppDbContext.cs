using Hospital.Application.Exceptions;
using Hospital.Application.Interfaces;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Notifications;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hospital.Persistence.Context;

/// <param name="dispatcher">
/// Domain event dağıtıcı. DI'dan gelir; tasarım zamanı (migration) gibi dağıtıcısız oluşturulursa olaylar dağıtılmaz.
/// </param>
/// <param name="protector">
/// Hassas veri şifreleyici (T.C. kimlik no). DI'dan gelir; tasarım zamanında yoksa dönüştürücü eklenmez (sütun türü değişmez,
/// migration aynı üretilir).
/// </param>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDomainEventDispatcher? dispatcher = null,
    ISensitiveDataProtector? protector = null) : DbContext(options)
{
    private const string UniqueViolation = "23505";
    private const string ExclusionViolation = "23P01";

    public DbSet<User> Users => Set<User>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Admin> Admins => Set<Admin>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Staff> StaffMembers => Set<Staff>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<HospitalBranch> Hospitals => Set<HospitalBranch>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<EmailOutboxMessage> EmailOutbox => Set<EmailOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Randevu çakışma kısıtı (exclusion constraint) için gerekli eklenti.
        modelBuilder.HasPostgresExtension("btree_gist");

        // Her entity'nin IEntityTypeConfiguration sınıfı Infrastructure/Features/** altında;
        // yeni feature eklemek bu dosyayı değiştirmez.
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        // KVKK: T.C. kimlik no veritabanında şifreli (AES-GCM) saklanır; arama/benzersizlik kör indeks sütunuyla yapılır.
        // Not: EF modeli süreç başına bir kez kurup önbelleğe alır; dönüştürücü ilk protector'ı yakalar. Süreçte tek (singleton)
        // protector olduğundan sorun yok; aynı süreçte farklı anahtarlı iki uygulama kurmak (test) ilkini kullanır.
        if (protector is not null)
        {
            modelBuilder.Entity<Patient>().Property(p => p.IdentityNumber).HasConversion(
                plain => protector.Protect(plain, SensitiveDataPurposes.PatientIdentityNumber),
                stored => protector.Unprotect(stored, SensitiveDataPurposes.PatientIdentityNumber));
        }
    }

    /// <summary>
    /// Eşzamanlı yarışlarda veritabanı kısıt ihlallerini (benzersizlik, exclusion) uygulama hatasına çevirir.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Olay dinleyicileri (ör. e-posta outbox satırı) değişikliklerle AYNI SaveChanges'te yazılır: atomik.
        await DispatchDomainEventsAsync(cancellationToken);
        StampSensitiveIndexes();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: UniqueViolation or ExclusionViolation
        } pg)
        {
            throw new DataConflictException(pg.ConstraintName ?? pg.SqlState);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Aynı satırı aynı anda iki istek değiştirdi (xmin eşzamanlılık belirteci): kaybeden 409 alır.
            throw new DataConflictException("concurrency");
        }
    }

    /// <summary>
    /// İzlenen aggregate'lerin biriken domain event'lerini toplar, aggregate'ten temizler ve dinleyicilere dağıtır.
    /// Dinleyicilerin eklediği kayıtlar aynı <c>base.SaveChangesAsync</c> ile yazılır.
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        if (dispatcher is null)
        {
            return;
        }

        var roots = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(root => root.DomainEvents.Count > 0)
            .ToList();

        var events = roots.SelectMany(root => root.DomainEvents).ToList();

        foreach (var root in roots)
        {
            root.ClearDomainEvents();
        }

        foreach (var domainEvent in events)
        {
            await dispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
    }

    /// <summary>
    /// Yeni eklenen hastada veya T.C. kimlik numarası gerçekten (yeniden) yazılan hastada kör indeksi (arama ve benzersizlik için)
    /// günceller. Başka alanı değişen (ör. son giriş zamanı) hastaya dokunmaz: eski düz metin kayıt, kimlik no şifrelenmeden
    /// indeks almış gibi görünmesin (yükseltme süreci kayıtları kör indeksi boşluğundan tanır).
    /// </summary>
    private void StampSensitiveIndexes()
    {
        if (protector is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<Patient>()
                     .Where(e => e.State == EntityState.Added
                                 || (e.State == EntityState.Modified && e.Property(p => p.IdentityNumber).IsModified)))
        {
            entry.Property(PatientIdentityHash).CurrentValue =
                protector.BlindIndex(entry.Entity.IdentityNumber, SensitiveDataPurposes.PatientIdentityNumber);
        }
    }

    /// <summary>Hasta T.C. kimlik no kör indeks sütununun gölge özellik adı.</summary>
    public const string PatientIdentityHash = "IdentityNumberHash";
}
