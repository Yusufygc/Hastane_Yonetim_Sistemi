using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Notifications.EventHandlers;

/// <summary>Randevu onayı: hastaya e-posta.</summary>
public sealed class AppointmentApprovedEmailHandler(INotificationReader reader, IEmailOutbox outbox, TimeProvider clock)
    : IDomainEventHandler<AppointmentApprovedEvent>
{
    public async Task HandleAsync(AppointmentApprovedEvent e, CancellationToken cancellationToken)
    {
        var recipients = await reader.GetAppointmentRecipientsAsync(e.PatientId, e.DoctorId, e.HospitalId, cancellationToken);
        if (recipients is null)
        {
            return;
        }

        await outbox.EnqueueAsync(
            AppointmentEmails.ToMessage(
                e.EventId, "patient", recipients.PatientEmail, recipients.PatientName,
                AppointmentEmails.Approved(recipients, e.ServiceName, e.StartAt), clock.GetUtcNow()),
            cancellationToken);
    }
}
