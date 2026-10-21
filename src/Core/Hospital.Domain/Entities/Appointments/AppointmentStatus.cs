namespace Hospital.Domain.Entities.Appointments;

public enum AppointmentStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
}

/// <summary>Durum makinesinde uygulanabilen işlemler (State Pattern geçişleri).</summary>
public enum AppointmentAction
{
    Approve = 1,
    Reject = 2,
    Cancel = 3,
    Complete = 4,
}

public static class AppointmentStatusNames
{
    /// <summary>Veritabanında saklanan değer (ör. PENDING). Exclusion kısıtı PENDING/APPROVED için geçerlidir.</summary>
    public static string From(AppointmentStatus status) => status.ToString().ToUpperInvariant();

    public static AppointmentStatus Parse(string value) => Enum.Parse<AppointmentStatus>(value, ignoreCase: true);

    public static bool TryParse(string? value, out AppointmentStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);

    /// <summary>Slotu tutan (çakışma kısıtına giren) durumlar.</summary>
    public static bool HoldsSlot(AppointmentStatus status) =>
        status is AppointmentStatus.Pending or AppointmentStatus.Approved;
}
