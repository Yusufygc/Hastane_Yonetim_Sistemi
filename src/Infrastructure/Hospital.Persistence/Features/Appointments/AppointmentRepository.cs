using Hospital.Application.Features.Appointments;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Scheduling;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Appointments;

internal sealed class AppointmentRepository(AppDbContext db) : IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TimeRange>> ListBookedRangesAsync(Guid doctorId, TimeRange window, CancellationToken cancellationToken)
    {
        var rows = await HoldingSlot()
            .Where(a => a.DoctorId == doctorId && a.StartAt < window.End && a.EndAt > window.Start)
            .Select(a => new { a.StartAt, a.EndAt })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TimeRange(r.StartAt, r.EndAt)).ToList();
    }

    public Task<bool> PatientHasOverlapAsync(Guid patientId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        HoldingSlot().AnyAsync(a => a.PatientId == patientId && a.StartAt < end && a.EndAt > start, cancellationToken);

    public Task<bool> DoctorHasFutureActiveAsync(Guid doctorId, Guid? serviceId, DateTimeOffset now, CancellationToken cancellationToken) =>
        HoldingSlot().AnyAsync(
            a => a.DoctorId == doctorId && a.EndAt > now && (serviceId == null || a.ServiceId == serviceId),
            cancellationToken);

    public Task<bool> DoctorHasActiveInRangeAsync(Guid doctorId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        HoldingSlot().AnyAsync(a => a.DoctorId == doctorId && a.StartAt < end && a.EndAt > start, cancellationToken);

    public Task<bool> UserHasFutureActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        HoldingSlot().AnyAsync(a => (a.PatientId == userId || a.DoctorId == userId) && a.EndAt > now, cancellationToken);

    public void Add(Appointment appointment) => db.Appointments.Add(appointment);

    /// <summary>Slotu tutan (PENDING/APPROVED) randevular; exclusion kısıtının WHERE koşuluyla aynı.</summary>
    private IQueryable<Appointment> HoldingSlot() =>
        db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved);
}
