using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.Persistence.Features.StaffMembers;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("staff");

        builder.Property(s => s.FirstName).HasMaxLength(Staff.NameMaxLength).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(Staff.NameMaxLength).IsRequired();
        builder.Property(s => s.Title).HasMaxLength(Staff.TitleMaxLength).IsRequired();

        builder.HasOne<HospitalBranch>()
            .WithMany()
            .HasForeignKey(s => s.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.HospitalId);
    }
}
