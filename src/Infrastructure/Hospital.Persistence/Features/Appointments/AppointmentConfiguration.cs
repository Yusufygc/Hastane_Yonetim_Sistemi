using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Appointments;

/// <summary>
/// Çakışma exclusion constraint'leri (ex_doctor_no_overlap, ex_patient_no_overlap) EF ile ifade edilemediği için
/// migration içinde ham SQL olarak tanımlanır (bkz. AppointmentOverlapConstraints migration'ı).
/// </summary>
internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointment", table =>
        {
            table.HasCheckConstraint("ck_appointment_range", "end_at > start_at");
            table.HasCheckConstraint(
                "ck_appointment_status",
                "status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED', 'COMPLETED')");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Status)
            .HasConversion(
                status => AppointmentStatusNames.From(status),
                value => Enum.Parse<AppointmentStatus>(value, true))
            .HasMaxLength(16)
            .IsRequired();

        // Doktor tablosu Faz 3'te gelir; o zamana kadar doctor_id yalnızca bir kimliktir (FK Faz 3/4 migration'ında eklenir).
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.DoctorId, a.StartAt });
        builder.HasIndex(a => new { a.PatientId, a.StartAt }).IsDescending(false, true);
    }
}
