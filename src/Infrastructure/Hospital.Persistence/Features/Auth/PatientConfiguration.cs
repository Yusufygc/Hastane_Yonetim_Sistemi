using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.Auth;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patient");

        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();

        // T.C. kimlik no uygulama katmanında şifrelenir (AES-GCM, bkz. AppDbContext); sütun şifreli metni taşır.
        // Benzersizlik ve arama şifre çözülmeden, deterministik kör indeksle (HMAC) yapılır.
        builder.Property(p => p.IdentityNumber).HasMaxLength(100).IsRequired();
        builder.Property<string?>(AppDbContext.PatientIdentityHash).HasMaxLength(64);
        builder.HasIndex(AppDbContext.PatientIdentityHash).IsUnique().HasDatabaseName("ix_patient_identity_number_hash");

        builder.Property(p => p.BloodType).HasMaxLength(8);
        builder.Property(p => p.Phone).HasMaxLength(32);
    }
}
