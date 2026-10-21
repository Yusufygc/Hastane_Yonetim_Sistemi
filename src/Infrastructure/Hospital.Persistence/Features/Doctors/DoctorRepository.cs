using Hospital.Application.Features.Doctors;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Doctors;

internal sealed class DoctorRepository(AppDbContext db) : IDoctorRepository
{
    public Task<Doctor?> GetByIdAsync(Guid id, DoctorInclude include, CancellationToken cancellationToken)
    {
        IQueryable<Doctor> query = db.Doctors;

        if (include.HasFlag(DoctorInclude.Services))
        {
            query = query.Include(d => d.Services);
        }

        if (include.HasFlag(DoctorInclude.Availabilities))
        {
            query = query.Include(d => d.Availabilities);
        }

        if (include.HasFlag(DoctorInclude.TimeOffs))
        {
            query = query.Include(d => d.TimeOffs);
        }

        // Birden çok koleksiyon yüklenirken kartezyen çarpımı önlemek için bölünmüş sorgu.
        return query.AsSplitQuery().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public Task<Doctor?> GetForSchedulingAsync(Guid id, CancellationToken cancellationToken) =>
        db.Doctors
            .AsNoTracking()
            .Include(d => d.Services)
            .Include(d => d.Availabilities)
            .Include(d => d.TimeOffs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> DiplomaExistsAsync(string diplomaNo, Guid? excludeId, CancellationToken cancellationToken) =>
        db.Doctors.AnyAsync(d => d.DiplomaNo == diplomaNo && d.Id != excludeId, cancellationToken);

    public void Add(Doctor doctor) => db.Doctors.Add(doctor);
}
