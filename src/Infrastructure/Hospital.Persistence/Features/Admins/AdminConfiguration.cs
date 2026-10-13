using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Admins;

internal sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.ToTable("admin", table =>
        {
            table.HasCheckConstraint("ck_admin_access_level", "access_level IN ('SUPER_ADMIN', 'BRANCH_MANAGER')");

            // Süper yöneticinin şubesi olmaz; şube yöneticisinin mutlaka şubesi olur.
            table.HasCheckConstraint("ck_admin_scope", "(access_level = 'SUPER_ADMIN') = (hospital_id IS NULL)");
        });

        builder.Property(a => a.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.LastName).HasMaxLength(100).IsRequired();

        builder.Property(a => a.AccessLevel)
            .HasConversion(
                level => AdminAccessLevelNames.From(level),
                value => AdminAccessLevelNames.Parse(value))
            .HasMaxLength(16)
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(a => a.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HospitalBranch>()
            .WithMany()
            .HasForeignKey(a => a.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
