using Hospital.Application.Features.Notifications;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Notifications;

internal sealed class NotificationReader(AppDbContext db) : INotificationReader
{
    public async Task<AppointmentRecipients?> GetAppointmentRecipientsAsync(
        Guid patientId,
        Guid doctorId,
        Guid hospitalId,
        CancellationToken cancellationToken) =>
        await (
                from p in db.Patients.AsNoTracking()
                from d in db.Doctors.AsNoTracking()
                from h in db.Hospitals.AsNoTracking()
                where p.Id == patientId && d.Id == doctorId && h.Id == hospitalId
                select new AppointmentRecipients(
                    p.Email,
                    p.FirstName + " " + p.LastName,
                    d.Email,
                    d.FirstName + " " + d.LastName,
                    h.Name,
                    h.TimeZoneId))
            .FirstOrDefaultAsync(cancellationToken);
}
