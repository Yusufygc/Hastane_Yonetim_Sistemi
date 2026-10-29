using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Auth;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_token");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        // SHA-256 hex (64 karakter); düz belirteç yalnız e-postada bulunur, veritabanında saklanmaz.
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // İyimser eşzamanlılık: aynı bağlantıyla iki eşzamanlı sıfırlamadan biri 409 alır (tek kullanım).
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kullanıcının kullanılabilir bağlantılarını bulmak için (yeni istek / sıfırlama sonrası geçersiz kılma).
        builder.HasIndex(t => t.UserId).HasFilter("used_at IS NULL");
    }
}
