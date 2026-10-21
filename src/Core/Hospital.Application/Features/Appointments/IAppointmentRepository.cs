using Hospital.Application.Security;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Scheduling;

namespace Hospital.Application.Features.Appointments;

/// <summary>Yazma tarafı: izlenen (tracked) randevu aggregate'i ve çakışma/denetim sorguları.</summary>
public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Doktorun slotu tutan (PENDING/APPROVED) randevularından verilen aralıkla kesişenler.</summary>
    Task<IReadOnlyList<TimeRange>> ListBookedRangesAsync(Guid doctorId, TimeRange window, CancellationToken cancellationToken);

    /// <summary>Hastanın aynı aralıkla kesişen PENDING/APPROVED randevusu var mı? (Hasta çift rezervasyon ön kontrolü.)</summary>
    Task<bool> PatientHasOverlapAsync(Guid patientId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);

    /// <summary>Doktorun bitişi <paramref name="now"/>'dan sonra olan PENDING/APPROVED randevusu var mı? <paramref name="serviceId"/> verilirse yalnız o hizmet için.</summary>
    Task<bool> DoctorHasFutureActiveAsync(Guid doctorId, Guid? serviceId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Doktorun verilen aralıkla kesişen PENDING/APPROVED randevusu var mı? (İzin eklerken.)</summary>
    Task<bool> DoctorHasActiveInRangeAsync(Guid doctorId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);

    /// <summary>Kullanıcının (hasta veya doktor olarak) bitişi <paramref name="now"/>'dan sonra olan PENDING/APPROVED randevusu var mı?</summary>
    Task<bool> UserHasFutureActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(Appointment appointment);
}

/// <summary>Okuma tarafı: DTO projeksiyonları. Listeler en fazla <see cref="AppointmentReaderLimits.MaxItems"/> kayıt döner.</summary>
public interface IAppointmentReader
{
    Task<AppointmentDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hastanın randevuları (yeniden eskiye).</summary>
    Task<IReadOnlyList<AppointmentDto>> ListForPatientAsync(Guid patientId, AppointmentStatus? status, CancellationToken cancellationToken);

    /// <summary>Doktorun randevuları (eskiden yeniye); <paramref name="from"/>/<paramref name="to"/> başlangıç zamanına uygulanır.</summary>
    Task<IReadOnlyList<AppointmentDto>> ListForDoctorAsync(
        Guid doctorId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        AppointmentStatus? status,
        CancellationToken cancellationToken);

    /// <summary>Yönetici kapsamındaki şubelerin randevuları (yeniden eskiye).</summary>
    Task<IReadOnlyList<AppointmentDto>> ListForAdminAsync(
        AdminScope scope,
        Guid? hospitalId,
        AppointmentStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}

public static class AppointmentReaderLimits
{
    public const int MaxItems = 200;
}

/// <param name="Status">PENDING, APPROVED, REJECTED, CANCELLED veya COMPLETED.</param>
public sealed record AppointmentDto(
    Guid Id,
    Guid HospitalId,
    string HospitalName,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid ServiceId,
    string ServiceName,
    int DurationMinutes,
    decimal Price,
    string Currency,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    string? PatientNote,
    string? DecisionReason,
    DateTimeOffset CreatedAt);

public static class AppointmentErrors
{
    public static readonly Wrappers.Error NotFound =
        Wrappers.Error.NotFound("appointment.not_found", "Randevu bulunamadı.");

    public static readonly Wrappers.Error DoctorNotFound =
        Wrappers.Error.NotFound("appointment.doctor_not_found", "Doktor bulunamadı veya şu anda randevu almıyor.");

    public static readonly Wrappers.Error ServiceInvalid =
        Wrappers.Error.Validation("appointment.service_invalid", "Hizmet bulunamadı, aktif değil veya doktorun şubesine ait değil.");

    public static readonly Wrappers.Error ServiceNotOffered =
        Wrappers.Error.Validation("appointment.service_not_offered", "Doktor bu hizmeti sunmuyor (yönetici onaylı hizmet değil).");

    public static readonly Wrappers.Error SlotUnavailable =
        Wrappers.Error.Conflict("appointment.slot_unavailable", "Seçilen saat müsait değil. Lütfen müsait saatlerden birini seçin.");

    /// <summary>Ön kontrolü geçen iki eşzamanlı istekten kaybeden (veritabanı exclusion kısıtı).</summary>
    public static readonly Wrappers.Error SlotTaken =
        Wrappers.Error.Conflict("appointment.slot_taken", "Bu saat az önce başka biri tarafından alındı. Lütfen başka bir saat seçin.");

    public static readonly Wrappers.Error PatientOverlap =
        Wrappers.Error.Conflict("appointment.patient_overlap", "Aynı saat aralığında başka bir randevunuz var.");

    public static readonly Wrappers.Error NotStarted =
        Wrappers.Error.Conflict("appointment.not_started", "Randevu başlamadan tamamlanamaz.");

    public static Wrappers.Error InvalidTransition(string stateLabel) =>
        Wrappers.Error.Conflict("appointment.invalid_transition", $"{stateLabel} durumundaki randevuya bu işlem uygulanamaz.");

    public static Wrappers.Error CancellationDeadlinePassed(int hours) =>
        Wrappers.Error.Conflict(
            "appointment.cancellation_deadline_passed",
            $"Onaylı randevu, başlamasına {hours} saatten az kala hasta tarafından iptal edilemez. Lütfen kliniği arayın.");
}
