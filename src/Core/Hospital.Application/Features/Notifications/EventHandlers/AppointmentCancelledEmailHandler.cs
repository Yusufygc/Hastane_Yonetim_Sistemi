using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Randevu iptali: <b>karşı tarafa</b> e-posta. Hasta iptal ederse doktora, doktor iptal ederse hastaya;
/// yönetici iptal ederse (ikisi de karşı taraf) her ikisine gider.
/// </summary>
public sealed class AppointmentCancelledEmailHandler(INotificationReader reader, IEmailOutbox outbox, TimeProvider clock)
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    public async Task HandleAsync(AppointmentCancelledEvent e, CancellationToken cancellationToken)
    {
        var recipients = await reader.GetAppointmentRecipientsAsync(e.PatientId, e.DoctorId, e.HospitalId, cancellationToken);
        if (recipients is null)
        {
            return;
        }

        var byPatient = e.CancelledBy == e.PatientId;
        var byDoctor = e.CancelledBy == e.DoctorId;
        var actor = byPatient ? "hasta" : byDoctor ? "doktor" : "yönetici";
        var now = clock.GetUtcNow();

        if (!byPatient)
        {
            await outbox.EnqueueAsync(
                AppointmentEmails.ToMessage(
                    e.EventId, "patient", recipients.PatientEmail, recipients.PatientName,
                    AppointmentEmails.CancelledForPatient(recipients, e.ServiceName, e.StartAt, e.Reason, actor), now),
                cancellationToken);
        }

        if (!byDoctor)
        {
            await outbox.EnqueueAsync(
                AppointmentEmails.ToMessage(
                    e.EventId, "doctor", recipients.DoctorEmail, recipients.DoctorName,
                    AppointmentEmails.CancelledForDoctor(recipients, e.ServiceName, e.StartAt, e.Reason, actor), now),
                cancellationToken);
        }
    }
}
