using Microsoft.EntityFrameworkCore;

namespace Hospital.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Her entity'nin IEntityTypeConfiguration sınıfı Infrastructure/Features/** altında;
        // yeni feature eklemek bu dosyayı değiştirmez.
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }
}
