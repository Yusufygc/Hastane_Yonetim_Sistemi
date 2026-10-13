namespace Hospital.Domain.Entities.Appointments;

public enum AppointmentStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
}

public static class AppointmentStatusNames
{
    /// <summary>Veritabanında saklanan değer (ör. PENDING). Exclusion kısıtı PENDING/APPROVED için geçerlidir.</summary>
    public static string From(AppointmentStatus status) => status.ToString().ToUpperInvariant();
}
