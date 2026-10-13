using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Appointments;

/// <summary>
/// Randevu (Faz 1B spike sürümü): çakışma koruması için gereken çekirdek alanlar.
/// Hizmet, hastane, snapshot alanları ve State Pattern geçişleri Faz 4'te eklenir.
/// </summary>
public sealed class Appointment : AggregateRoot
{
    /// <summary>EF Core materialization için.</summary>
    private Appointment()
    {
    }

    private Appointment(
        Guid id,
        Guid patientId,
        Guid doctorId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        DateTimeOffset now)
        : base(id)
    {
        PatientId = patientId;
        DoctorId = doctorId;
        StartAt = startAt;
        EndAt = endAt;
        Status = AppointmentStatus.Pending;
        CreatedAt = now;
    }

    public Guid PatientId { get; private set; }

    public Guid DoctorId { get; private set; }

    /// <summary>UTC. Aralık yarı açıktır: [StartAt, EndAt).</summary>
    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Yeni randevu talebi oluşturur (<see cref="AppointmentStatus.Pending"/>; slotu rezerve eder).</summary>
    public static Appointment Request(
        Guid patientId,
        Guid doctorId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        DateTimeOffset now)
    {
        if (patientId == Guid.Empty || doctorId == Guid.Empty)
        {
            throw new DomainException("Hasta ve doktor belirtilmelidir.");
        }

        if (endAt <= startAt)
        {
            throw new DomainException("Randevu bitişi başlangıçtan sonra olmalıdır.");
        }

        return new Appointment(
            Guid.CreateVersion7(),
            patientId,
            doctorId,
            startAt.ToUniversalTime(),
            endAt.ToUniversalTime(),
            now.ToUniversalTime());
    }
}
