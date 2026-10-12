using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Auth;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user_account", table =>
            table.HasCheckConstraint("ck_user_account_role", "role IN ('ADMIN', 'DOCTOR', 'STAFF', 'PATIENT')"));

        // Table Per Type: alt tipler (patient, doctor, ...) aynı birincil anahtarı paylaşır.
        builder.UseTptMappingStrategy();

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();

        builder.Property(u => u.Role)
            .HasConversion(
                role => RoleNames.From(role),
                value => Enum.Parse<UserRole>(value, true))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
    }
}
