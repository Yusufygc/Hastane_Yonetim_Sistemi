using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Notifications.EventHandlers;

/// <summary>Randevu reddi: hastaya red nedeniyle birlikte e-posta.</summary>
public sealed class AppointmentRejectedEmailHandler(INotificationReader reader, IEmailOutbox outbox, TimeProvider clock)
    : IDomainEventHandler<AppointmentRejectedEvent>
{
    public async Task HandleAsync(AppointmentRejectedEvent e, CancellationToken cancellationToken)
    {
        var recipients = await reader.GetAppointmentRecipientsAsync(e.PatientId, e.DoctorId, e.HospitalId, cancellationToken);
        if (recipients is null)
        {
            return;
        }

        await outbox.EnqueueAsync(
            AppointmentEmails.ToMessage(
                e.EventId, "patient", recipients.PatientEmail, recipients.PatientName,
                AppointmentEmails.Rejected(recipients, e.ServiceName, e.StartAt, e.Reason), clock.GetUtcNow()),
            cancellationToken);
    }
}
