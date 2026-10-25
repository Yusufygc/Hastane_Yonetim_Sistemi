using Hospital.Application.Exceptions;
using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Appointments;

/// <summary>Bekleyen talep zaman aşımı ayarları (<c>Appointment:PendingExpiry</c>).</summary>
/// <param name="Enabled">Kapalıysa arka plan işleyici başlamaz.</param>
/// <param name="After">Talep bu süre içinde karara bağlanmazsa otomatik reddedilir.</param>
public sealed record PendingExpirySettings(bool Enabled, TimeSpan After, TimeSpan PollInterval, int BatchSize)
{
    public static PendingExpirySettings Default { get; } = new(true, TimeSpan.FromHours(48), TimeSpan.FromMinutes(5), 50);
}

/// <summary>
/// Doktor yanıt vermeyen <b>bekleyen</b> randevu taleplerini otomatik düşürür: talep <see cref="PendingExpirySettings.After"/>
/// süresinden eskiyse veya başlangıç zamanı geçmişse reddedilmiş sayılır (slot serbest kalır, hastaya e-posta gider).
/// Her randevu ayrı kapsam/transaction'da işlenmelidir (<see cref="ExpireAsync"/>): aynı anda doktor karar verirse
/// yalnız o randevu atlanır, diğerleri etkilenmez.
/// </summary>
public sealed class PendingAppointmentExpirer(
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    PendingExpirySettings settings,
    TimeProvider clock)
{
    public Task<IReadOnlyList<Guid>> FindDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return appointments.ListDuePendingIdsAsync(now - settings.After, now, settings.BatchSize, cancellationToken);
    }

    /// <summary>Randevu hâlâ bekliyor ve süresi dolmuşsa düşürür; aksi halde (veya eşzamanlı karar varsa) <c>false</c>.</summary>
    public async Task<bool> ExpireAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken);

        if (appointment is not { Status: AppointmentStatus.Pending }
            || (appointment.CreatedAt > now - settings.After && appointment.StartAt > now))
        {
            return false;
        }

        appointment.ExpireWithoutResponse(now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DataConflictException)
        {
            // Doktor/hasta aynı anda karar verdi (xmin eşzamanlılık belirteci): o karar geçerli kalır.
            return false;
        }
    }
}
