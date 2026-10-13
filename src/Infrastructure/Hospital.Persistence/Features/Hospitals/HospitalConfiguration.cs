using Hospital.Domain.Entities.Facility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Hospitals;

internal sealed class HospitalConfiguration : IEntityTypeConfiguration<HospitalBranch>
{
    public void Configure(EntityTypeBuilder<HospitalBranch> builder)
    {
        builder.ToTable("hospital");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.Name).HasMaxLength(HospitalBranch.NameMaxLength).IsRequired();
        builder.Property(h => h.Address).HasMaxLength(HospitalBranch.AddressMaxLength);
        builder.Property(h => h.Phone).HasMaxLength(HospitalBranch.PhoneMaxLength);
        builder.Property(h => h.TimeZoneId).HasColumnName("time_zone").HasMaxLength(64).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(h => h.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.OrganizationId, h.Name }).IsUnique();

        builder.HasMany(h => h.WorkingHours)
            .WithOne()
            .HasForeignKey(w => w.HospitalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(h => h.WorkingHours).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class HospitalWorkingHoursConfiguration : IEntityTypeConfiguration<HospitalWorkingHours>
{
    public void Configure(EntityTypeBuilder<HospitalWorkingHours> builder)
    {
        builder.ToTable("hospital_working_hours", table =>
        {
            table.HasCheckConstraint("ck_hospital_working_hours_day", "day_of_week BETWEEN 0 AND 6");
            table.HasCheckConstraint("ck_hospital_working_hours_range", "close_time > open_time");
        });

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Day)
            .HasColumnName("day_of_week")
            .HasConversion<short>();

        builder.HasIndex(w => new { w.HospitalId, w.Day }).IsUnique();
    }
}
