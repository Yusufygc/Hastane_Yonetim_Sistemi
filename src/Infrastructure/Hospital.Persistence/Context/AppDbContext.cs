using Hospital.Application.Exceptions;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hospital.Persistence.Context;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const string UniqueViolation = "23505";
    private const string ExclusionViolation = "23P01";

    public DbSet<User> Users => Set<User>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Admin> Admins => Set<Admin>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<HospitalBranch> Hospitals => Set<HospitalBranch>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Randevu çakışma kısıtı (exclusion constraint) için gerekli eklenti.
        modelBuilder.HasPostgresExtension("btree_gist");

        // Her entity'nin IEntityTypeConfiguration sınıfı Infrastructure/Features/** altında;
        // yeni feature eklemek bu dosyayı değiştirmez.
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }

    /// <summary>
    /// Eşzamanlı yarışlarda veritabanı kısıt ihlallerini (benzersizlik, exclusion) uygulama hatasına çevirir.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
    }
}
