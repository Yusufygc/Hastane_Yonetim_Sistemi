using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

public enum DoctorServiceStatus
{
    /// <summary>Doktor talep etti, yönetici onayı bekliyor. Slot üretmez.</summary>
    Pending = 1,

    /// <summary>Doktor bu hizmeti sunar; randevu alınabilir.</summary>
    Approved = 2,

    Rejected = 3,
}

public static class DoctorServiceStatusNames
{
    public static string From(DoctorServiceStatus status) => status.ToString().ToUpperInvariant();
}

/// <summary>
/// Doktor-hizmet ilişkisi (DOCTOR_SERVICE, bileşik anahtar: doctor_id + service_id). Doktor kendi hastanesindeki bir hizmeti
/// <b>talep eder</b> (Pending), yönetici onaylar/reddeder; yönetici doğrudan da atayabilir (Approved).
/// Yalnızca <see cref="DoctorServiceStatus.Approved"/> kayıtlar randevu alınabilirliğini belirler. Geçişleri <see cref="Doctor"/> yönetir.
/// </summary>
public sealed class DoctorServiceLink
{
    /// <summary>EF Core materialization için.</summary>
    private DoctorServiceLink()
    {
    }

    internal DoctorServiceLink(Guid doctorId, Guid serviceId, Guid requestedBy, DateTimeOffset now)
    {
        DoctorId = doctorId;
        ServiceId = serviceId;
        RequestedBy = requestedBy;
        Status = DoctorServiceStatus.Pending;
        CreatedAt = now;
    }

    public Guid DoctorId { get; private set; }

    public Guid ServiceId { get; private set; }

    public DoctorServiceStatus Status { get; private set; }

    /// <summary>Talebi/atamayı yapan kullanıcı (doktorun kendisi veya yönetici).</summary>
    public Guid RequestedBy { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Reddedilmiş talebi yeniden bekleyen duruma alır (doktor tekrar talep etti).</summary>
    internal void Reopen(Guid requestedBy, DateTimeOffset now)
    {
        if (Status != DoctorServiceStatus.Rejected)
        {
            throw new DomainException("Yalnızca reddedilmiş talep yeniden açılabilir.");
        }

        Status = DoctorServiceStatus.Pending;
        RequestedBy = requestedBy;
        CreatedAt = now;
        DecidedBy = null;
        DecidedAt = null;
    }

    internal void Approve(Guid decidedBy, DateTimeOffset now)
    {
        if (Status == DoctorServiceStatus.Approved)
        {
            return; // idempotent
        }

        Status = DoctorServiceStatus.Approved;
        DecidedBy = decidedBy;
        DecidedAt = now;
    }

    internal void Reject(Guid decidedBy, DateTimeOffset now)
    {
        if (Status != DoctorServiceStatus.Pending)
        {
            throw new DomainException("Yalnızca onay bekleyen talep reddedilebilir.");
        }

        Status = DoctorServiceStatus.Rejected;
        DecidedBy = decidedBy;
        DecidedAt = now;
    }
}
