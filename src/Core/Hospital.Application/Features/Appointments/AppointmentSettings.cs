namespace Hospital.Application.Features.Appointments;

/// <summary>Randevu kuralları ayarları (<c>Appointment</c> yapılandırma bölümü).</summary>
/// <param name="SlotGridMinutes">Slot başlangıç ızgarası (dk).</param>
/// <param name="MinLeadMinutes">En az hazırlık süresi (dk): bundan yakın başlangıçlar alınamaz.</param>
/// <param name="MaxBookingHorizonDays">Bugünden itibaren en fazla kaç gün ilerisine randevu alınabilir.</param>
/// <param name="CancellationDeadlineHours">Hasta, onaylı randevuyu başlamasına bu süreden az kala iptal edemez.</param>
public sealed record AppointmentSettings(
    int SlotGridMinutes,
    int MinLeadMinutes,
    int MaxBookingHorizonDays,
    int CancellationDeadlineHours)
{
    public static AppointmentSettings Default { get; } = new(
        SlotGridMinutes: 15,
        MinLeadMinutes: 60,
        MaxBookingHorizonDays: 60,
        CancellationDeadlineHours: 2);

    public TimeSpan MinLead => TimeSpan.FromMinutes(MinLeadMinutes);

    public TimeSpan CancellationDeadline => TimeSpan.FromHours(CancellationDeadlineHours);
}
