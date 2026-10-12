using Hospital.Domain.Entities.Users;
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

        builder.Property(p => p.IdentityNumber).HasMaxLength(TurkishIdentityNumber.Length).IsFixedLength().IsRequired();
        builder.HasIndex(p => p.IdentityNumber).IsUnique();

        builder.Property(p => p.BloodType).HasMaxLength(8);
        builder.Property(p => p.Phone).HasMaxLength(32);
    }
}
