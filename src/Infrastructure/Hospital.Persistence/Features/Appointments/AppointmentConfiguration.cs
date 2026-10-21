using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Appointments;

/// <summary>
/// Çakışma exclusion constraint'leri (ex_doctor_no_overlap, ex_patient_no_overlap) EF ile ifade edilemediği için
/// migration içinde ham SQL olarak tanımlanır (bkz. AppointmentOverlapConstraints migration'ı).
/// Eşzamanlı durum geçişleri PostgreSQL <c>xmin</c> sistem sütunuyla iyimser eşzamanlılık belirteci üzerinden yakalanır.
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
            table.HasCheckConstraint("ck_appointment_snapshot", "duration_minutes_snapshot > 0 AND price_snapshot >= 0");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Türetilmiş (hesaplanan) özellikler veritabanına eşlenmez.
        builder.Ignore(a => a.State);
        builder.Ignore(a => a.HoldsSlot);

        builder.Property(a => a.Status)
            .HasConversion(
                status => AppointmentStatusNames.From(status),
                value => AppointmentStatusNames.Parse(value))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(a => a.ServiceNameSnapshot).HasMaxLength(Service.NameMaxLength).IsRequired();
        builder.Property(a => a.PriceSnapshot).HasPrecision(12, 2);
        builder.Property(a => a.CurrencySnapshot).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(a => a.PatientNote).HasMaxLength(Appointment.NoteMaxLength);
        builder.Property(a => a.DecisionReason).HasMaxLength(Appointment.ReasonMaxLength);

        // İyimser eşzamanlılık: satır her değiştiğinde xmin değişir; iki doktor/yönetici aynı anda karar verirse biri 409 alır.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<HospitalBranch>()
            .WithMany()
            .HasForeignKey(a => a.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Doctor>()
            .WithMany()
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.StatusChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.DoctorId, a.StartAt });
        builder.HasIndex(a => new { a.PatientId, a.StartAt }).IsDescending(false, true);
        builder.HasIndex(a => new { a.HospitalId, a.Status, a.StartAt });
    }
}
