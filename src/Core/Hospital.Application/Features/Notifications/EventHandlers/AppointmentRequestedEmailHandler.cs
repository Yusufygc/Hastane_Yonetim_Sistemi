using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Notifications.EventHandlers;

/// <summary>Randevu talebi: hastaya "alındı", doktora "onayınızı bekliyor" e-postası (outbox satırı, aynı transaction).</summary>
public sealed class AppointmentRequestedEmailHandler(INotificationReader reader, IEmailOutbox outbox, TimeProvider clock)
    : IDomainEventHandler<AppointmentRequestedEvent>
{
    public async Task HandleAsync(AppointmentRequestedEvent e, CancellationToken cancellationToken)
    {
        var recipients = await reader.GetAppointmentRecipientsAsync(e.PatientId, e.DoctorId, e.HospitalId, cancellationToken);
        if (recipients is null)
        {
            return;
        }

        var now = clock.GetUtcNow();

        await outbox.EnqueueAsync(
            AppointmentEmails.ToMessage(
                e.EventId, "patient", recipients.PatientEmail, recipients.PatientName,
                AppointmentEmails.RequestedForPatient(recipients, e.ServiceName, e.StartAt), now),
            cancellationToken);

        await outbox.EnqueueAsync(
            AppointmentEmails.ToMessage(
                e.EventId, "doctor", recipients.DoctorEmail, recipients.DoctorName,
                AppointmentEmails.RequestedForDoctor(recipients, e.ServiceName, e.StartAt), now),
            cancellationToken);
    }
}
