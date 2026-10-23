using Hospital.Domain.Common;
using Hospital.Domain.Entities.Appointments.States;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Domain.Entities.Appointments;

/// <summary>Randevu anındaki hizmet bilgisi: hizmet sonradan değişse de randevudaki ad/süre/ücret sabit kalır.</summary>
public readonly record struct ServiceSnapshot(Guid ServiceId, string Name, int DurationMinutes, decimal Price, string Currency)
{
    public static ServiceSnapshot From(Service service) =>
        new(service.Id, service.Name, service.DurationMinutes, service.Price, service.Currency);
}

/// <summary>
/// Randevu aggregate'i. Durum geçişleri <see cref="AppointmentState"/> (State Pattern) üzerinden yapılır; geçersiz geçiş
/// <see cref="DomainException"/> fırlatır. <c>PENDING</c> ve <c>APPROVED</c> slotu tutar (veritabanında exclusion kısıtı).
/// </summary>
public sealed class Appointment : AggregateRoot
{
    public const int NoteMaxLength = 500;
    public const int ReasonMaxLength = 500;

    /// <summary>EF Core materialization için.</summary>
    private Appointment()
    {
        ServiceNameSnapshot = null!;
        CurrencySnapshot = null!;
    }

    private Appointment(
        Guid id,
        Guid hospitalId,
        Guid patientId,
        Guid doctorId,
        ServiceSnapshot service,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string? patientNote,
        DateTimeOffset now)
        : base(id)
    {
        HospitalId = hospitalId;
        PatientId = patientId;
        DoctorId = doctorId;
        ServiceId = service.ServiceId;
        ServiceNameSnapshot = service.Name;
        DurationMinutesSnapshot = service.DurationMinutes;
        PriceSnapshot = service.Price;
        CurrencySnapshot = service.Currency;
        StartAt = startAt;
        EndAt = endAt;
        PatientNote = patientNote;
        Status = AppointmentStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid HospitalId { get; private set; }

    public Guid PatientId { get; private set; }

    public Guid DoctorId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string ServiceNameSnapshot { get; private set; }

    public int DurationMinutesSnapshot { get; private set; }

    public decimal PriceSnapshot { get; private set; }

    public string CurrencySnapshot { get; private set; }

    /// <summary>UTC. Aralık yarı açıktır: [StartAt, EndAt).</summary>
    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? PatientNote { get; private set; }

    /// <summary>Red veya iptal nedeni.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>Durumu en son değiştiren kullanıcı (doktor, hasta veya yönetici).</summary>
    public Guid? StatusChangedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Durum makinesi (State Pattern); <see cref="Status"/> değerinden türetilir.</summary>
    public AppointmentState State => AppointmentState.For(Status);

    /// <summary>Slotu tutuyor mu? (PENDING veya APPROVED)</summary>
    public bool HoldsSlot => AppointmentStatusNames.HoldsSlot(Status);

    /// <summary>Yeni randevu talebi oluşturur (<see cref="AppointmentStatus.Pending"/>; slotu rezerve eder).</summary>
    public static Appointment Request(
        Guid patientId,
        Guid doctorId,
        Guid hospitalId,
        ServiceSnapshot service,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string? patientNote,
        DateTimeOffset now)
    {
        if (patientId == Guid.Empty || doctorId == Guid.Empty || hospitalId == Guid.Empty || service.ServiceId == Guid.Empty)
        {
            throw new DomainException("Hasta, doktor, hastane ve hizmet belirtilmelidir.");
        }

        if (endAt <= startAt)
        {
            throw new DomainException("Randevu bitişi başlangıçtan sonra olmalıdır.");
        }

        var appointment = new Appointment(
            Guid.CreateVersion7(),
            hospitalId,
            patientId,
            doctorId,
            service,
            startAt.ToUniversalTime(),
            endAt.ToUniversalTime(),
            Guard.OptionalText(patientNote, "Not", NoteMaxLength),
            now.ToUniversalTime());

        appointment.Raise(new AppointmentRequestedEvent(
            appointment.Id, patientId, doctorId, hospitalId, service.Name, appointment.StartAt, appointment.EndAt));

        return appointment;
    }

    /// <summary>İşlem mevcut durumda uygulanabilir mi? (Randevu başlama zamanı gibi kurallar için ayrıca bkz. <see cref="HasStarted"/>.)</summary>
    public bool Allows(AppointmentAction action) => State.Allows(action);

    public bool HasStarted(DateTimeOffset now) => now >= StartAt;

    /// <summary>
    /// Hastanın iptal süre sınırı: randevuya <paramref name="deadline"/> kadar veya daha az kala <b>onaylı</b> randevu
    /// hasta tarafından iptal edilemez. Bekleyen randevu her zaman iptal edilebilir.
    /// </summary>
    public bool IsWithinCancellationDeadline(DateTimeOffset now, TimeSpan deadline) =>
        Status == AppointmentStatus.Approved && StartAt - now < deadline;

    public void Approve(Guid doctorId, DateTimeOffset now)
    {
        Transition(AppointmentAction.Approve, doctorId, reason: null, now);
        Raise(new AppointmentApprovedEvent(Id, PatientId, DoctorId, HospitalId, ServiceNameSnapshot, StartAt));
    }

    public void Reject(Guid doctorId, string reason, DateTimeOffset now)
    {
        var text = Guard.NotBlank(reason, "Red nedeni", ReasonMaxLength);
        Transition(AppointmentAction.Reject, doctorId, text, now);
        Raise(new AppointmentRejectedEvent(Id, PatientId, DoctorId, HospitalId, ServiceNameSnapshot, StartAt, text));
    }

    public void Cancel(Guid actorId, string reason, DateTimeOffset now)
    {
        var text = Guard.NotBlank(reason, "İptal nedeni", ReasonMaxLength);
        Transition(AppointmentAction.Cancel, actorId, text, now);
        Raise(new AppointmentCancelledEvent(Id, PatientId, DoctorId, HospitalId, ServiceNameSnapshot, StartAt, actorId, text));
    }

    /// <summary>Yalnızca onaylı ve başlamış randevu tamamlanır.</summary>
    public void Complete(Guid doctorId, DateTimeOffset now)
    {
        if (!HasStarted(now))
        {
            throw new DomainException("Randevu başlamadan tamamlanamaz.");
        }

        Transition(AppointmentAction.Complete, doctorId, reason: null, now);
    }

    private void Transition(AppointmentAction action, Guid changedBy, string? reason, DateTimeOffset now)
    {
        var next = State.Next(action);

        Status = next;
        StatusChangedBy = Guard.NotEmpty(changedBy, "İşlemi yapan kullanıcı");
        UpdatedAt = now.ToUniversalTime();

        if (reason is not null)
        {
            DecisionReason = reason;
        }
    }
}
