using Hospital.Domain.Entities.Facility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Services;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("service", table =>
        {
            table.HasCheckConstraint("ck_service_duration", $"duration_minutes > 0 AND duration_minutes <= {Service.MaxDurationMinutes}");
            table.HasCheckConstraint("ck_service_price", "price >= 0");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(Service.NameMaxLength).IsRequired();
        builder.Property(s => s.Price).HasPrecision(12, 2);
        builder.Property(s => s.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.HasOne<HospitalBranch>()
            .WithMany()
            .HasForeignKey(s => s.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.HospitalId, s.Name }).IsUnique();
        builder.HasIndex(s => new { s.HospitalId, s.IsActive });
    }
}
