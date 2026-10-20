using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Doctors;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("doctor");

        builder.Property(d => d.FirstName).HasMaxLength(Doctor.NameMaxLength).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(Doctor.NameMaxLength).IsRequired();
        builder.Property(d => d.Specialty).HasMaxLength(Doctor.SpecialtyMaxLength).IsRequired();
        builder.Property(d => d.DiplomaNo).HasMaxLength(Doctor.DiplomaMaxLength).IsRequired();
        builder.Property(d => d.Phone).HasMaxLength(Doctor.PhoneMaxLength);

        builder.HasIndex(d => d.DiplomaNo).IsUnique();
        builder.HasIndex(d => d.DepartmentId);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(d => d.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Services)
            .WithOne()
            .HasForeignKey(s => s.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Availabilities)
            .WithOne()
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.TimeOffs)
            .WithOne()
            .HasForeignKey(t => t.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(d => d.Availabilities).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(d => d.TimeOffs).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DoctorServiceLinkConfiguration : IEntityTypeConfiguration<DoctorServiceLink>
{
    public void Configure(EntityTypeBuilder<DoctorServiceLink> builder)
    {
        builder.ToTable("doctor_service", table =>
            table.HasCheckConstraint("ck_doctor_service_status", "status IN ('PENDING', 'APPROVED', 'REJECTED')"));

        // Bileşik anahtar (doctor_id, service_id): aynı hizmet için ikinci kayıt veritabanında da engellenir.
        builder.HasKey(l => new { l.DoctorId, l.ServiceId });

        builder.Property(l => l.Status)
            .HasConversion(
                status => DoctorServiceStatusNames.From(status),
                value => Enum.Parse<DoctorServiceStatus>(value, true))
            .HasMaxLength(16)
            .IsRequired();

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(l => l.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.DecidedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.ServiceId);
        builder.HasIndex(l => new { l.Status, l.CreatedAt });
    }
}

/// <summary>
/// Çakışma exclusion constraint'i (ex_doctor_availability_no_overlap) EF ile ifade edilemediği için migration içinde
/// ham SQL olarak tanımlanır (bkz. DoctorsAndStaff migration'ı).
/// </summary>
internal sealed class DoctorAvailabilityConfiguration : IEntityTypeConfiguration<DoctorAvailability>
{
    public void Configure(EntityTypeBuilder<DoctorAvailability> builder)
    {
        builder.ToTable("doctor_availability", table =>
        {
            table.HasCheckConstraint("ck_doctor_availability_day", "day_of_week BETWEEN 0 AND 6");
            table.HasCheckConstraint("ck_doctor_availability_range", "end_time > start_time");
            table.HasCheckConstraint("ck_doctor_availability_validity", "valid_to IS NULL OR valid_to >= valid_from");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Day)
            .HasColumnName("day_of_week")
            .HasConversion<short>();

        builder.HasIndex(a => new { a.DoctorId, a.Day });
    }
}

internal sealed class DoctorTimeOffConfiguration : IEntityTypeConfiguration<DoctorTimeOff>
{
    public void Configure(EntityTypeBuilder<DoctorTimeOff> builder)
    {
        builder.ToTable("doctor_time_off", table =>
            table.HasCheckConstraint("ck_doctor_time_off_range", "end_at > start_at"));

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Reason).HasMaxLength(DoctorTimeOff.ReasonMaxLength);

        builder.HasIndex(t => new { t.DoctorId, t.StartAt });
    }
}
