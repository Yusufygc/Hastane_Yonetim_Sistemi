using Hospital.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Notifications;

internal sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("email_outbox", table =>
        {
            table.HasCheckConstraint("ck_email_outbox_status", "status IN ('PENDING', 'SENT', 'FAILED')");
            table.HasCheckConstraint("ck_email_outbox_attempts", "attempt_count >= 0");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.DedupKey).HasMaxLength(EmailOutboxMessage.DedupKeyMaxLength).IsRequired();
        builder.Property(m => m.ToAddress).HasMaxLength(EmailOutboxMessage.AddressMaxLength).IsRequired();
        builder.Property(m => m.ToName).HasMaxLength(EmailOutboxMessage.NameMaxLength);
        builder.Property(m => m.Subject).HasMaxLength(EmailOutboxMessage.SubjectMaxLength).IsRequired();
        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.TemplateKey).HasMaxLength(EmailOutboxMessage.TemplateKeyMaxLength).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(EmailOutboxMessage.ErrorMaxLength);

        builder.Property(m => m.Status)
            .HasConversion(
                status => EmailStatusNames.From(status),
                value => EmailStatusNames.Parse(value))
            .HasMaxLength(16)
            .IsRequired();

        // Aynı olay + alıcı için ikinci satır veritabanında da engellenir (tekrar gönderim yok).
        builder.HasIndex(m => m.DedupKey).IsUnique();

        // İşleyici yalnız bekleyenleri tarar: kısmi indeks, gönderilmiş milyonlarca satırı taramaz.
        builder.HasIndex(m => new { m.NextAttemptAt, m.CreatedAt })
            .HasFilter("status = 'PENDING'")
            .HasDatabaseName("ix_email_outbox_pending");
    }
}
