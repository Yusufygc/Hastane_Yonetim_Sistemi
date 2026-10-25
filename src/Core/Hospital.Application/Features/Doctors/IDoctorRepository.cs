using Hospital.Application.Parameters;
using Hospital.Application.Security;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Doctors;

/// <summary>Doktor aggregate'inin hangi alt koleksiyonlarının yükleneceği (gereksiz join'den kaçınmak için).</summary>
[Flags]
public enum DoctorInclude
{
    None = 0,
    Services = 1,
    Availabilities = 2,
    TimeOffs = 4,
}

/// <summary>Yazma tarafı: izlenen (tracked) doktor aggregate'ini döner.</summary>
public interface IDoctorRepository
{
    Task<Doctor?> GetByIdAsync(Guid id, DoctorInclude include, CancellationToken cancellationToken);

    /// <summary>
    /// Randevu için salt okunur yükleme: izlenmeyen doktor + hizmetler + uygunluklar + izinler (slot hesabı ve oluşturma ön kontrolü).
    /// </summary>
    Task<Doctor?> GetForSchedulingAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DiplomaExistsAsync(string diplomaNo, Guid? excludeId, CancellationToken cancellationToken);

    void Add(Doctor doctor);
}

/// <summary>Okuma tarafı: DTO projeksiyonları. Yönetici sorguları kapsama göre süzülür.</summary>
public interface IDoctorReader
{
    Task<PagedList<DoctorDto>> ListAsync(AdminScope scope, Guid? hospitalId, Guid? departmentId, PageRequest page, CancellationToken cancellationToken);

    Task<DoctorDetailDto?> GetAsync(Guid id, AdminScope scope, CancellationToken cancellationToken);

    /// <summary>Doktorun kendi profili (kapsam denetimi gerekmez; kimlik belirteçten gelir).</summary>
    Task<DoctorDetailDto?> GetOwnAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorServiceDto>> ListServicesAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorAvailabilityDto>> ListAvailabilitiesAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorTimeOffDto>> ListTimeOffsAsync(Guid doctorId, CancellationToken cancellationToken);

    /// <param name="status">PENDING, APPROVED veya REJECTED.</param>
    Task<PagedList<DoctorServiceRequestDto>> ListServiceRequestsAsync(AdminScope scope, string status, PageRequest page, CancellationToken cancellationToken);
}

public sealed record DoctorDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid HospitalId,
    Guid DepartmentId,
    string DepartmentName,
    string Specialty,
    string DiplomaNo,
    string? Phone,
    bool IsActive);

public sealed record DoctorDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid HospitalId,
    Guid DepartmentId,
    string DepartmentName,
    string Specialty,
    string DiplomaNo,
    string? Phone,
    bool IsActive,
    IReadOnlyList<DoctorServiceDto> Services);

/// <param name="Status">PENDING, APPROVED veya REJECTED.</param>
public sealed record DoctorServiceDto(Guid ServiceId, string ServiceName, string Status, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);

public sealed record DoctorServiceRequestDto(
    Guid DoctorId,
    string DoctorName,
    Guid HospitalId,
    Guid ServiceId,
    string ServiceName,
    string Status,
    DateTimeOffset RequestedAt);

/// <param name="Day">0 = Pazar … 6 = Cumartesi.</param>
public sealed record DoctorAvailabilityDto(Guid Id, DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record DoctorTimeOffDto(Guid Id, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Reason);

public static class DoctorStatusFilter
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Pending, Approved, Rejected };
}

public static class DoctorErrors
{
    public static readonly Wrappers.Error NotFound =
        Wrappers.Error.NotFound("doctor.not_found", "Doktor bulunamadı.");

    public static readonly Wrappers.Error DiplomaTaken =
        Wrappers.Error.Conflict("doctor.diploma_taken", "Bu diploma numarası başka bir doktora ait.");

    public static readonly Wrappers.Error DepartmentInvalid =
        Wrappers.Error.Validation("doctor.department_invalid", "Bölüm aktif olmalı ve doktorun şubesine ait olmalıdır.");

    public static readonly Wrappers.Error ServiceInvalid =
        Wrappers.Error.Validation("doctor.service_invalid", "Hizmet doktorun şubesine ait ve aktif olmalıdır.");

    public static readonly Wrappers.Error ServiceLinkNotFound =
        Wrappers.Error.NotFound("doctor.service_link_not_found", "Doktorun bu hizmet için bir talebi/ataması yok.");

    public static readonly Wrappers.Error ServiceAlreadyApproved =
        Wrappers.Error.Conflict("doctor.service_already_approved", "Bu hizmet zaten onaylı.");

    public static readonly Wrappers.Error ServiceAlreadyRequested =
        Wrappers.Error.Conflict("doctor.service_already_requested", "Bu hizmet için talep zaten onay bekliyor.");

    public static readonly Wrappers.Error ServiceNotPending =
        Wrappers.Error.Conflict("doctor.service_not_pending", "Yalnızca onay bekleyen talep reddedilebilir.");

    public static readonly Wrappers.Error ServiceApprovedLocked =
        Wrappers.Error.Conflict("doctor.service_approved_locked", "Onaylı hizmeti yalnızca yönetici kaldırabilir.");

    public static readonly Wrappers.Error ServiceHasFutureAppointments =
        Wrappers.Error.Conflict("doctor.service_has_future_appointments", "Bu hizmet için gelecekte aktif (bekleyen/onaylı) randevu var; önce randevuları iptal edin.");

    public static readonly Wrappers.Error TimeOffHasAppointments =
        Wrappers.Error.Conflict("doctor.time_off_has_appointments", "Bu aralıkta aktif (bekleyen/onaylı) randevu var; önce randevuları iptal edin.");

    public static readonly Wrappers.Error AvailabilityNotFound =
        Wrappers.Error.NotFound("doctor.availability_not_found", "Uygunluk kaydı bulunamadı.");

    public static readonly Wrappers.Error AvailabilityOutsideHours =
        Wrappers.Error.Validation("doctor.availability_outside_hours", "Uygunluk aralığı şubenin o günkü çalışma saatleri içinde olmalıdır (şube o gün kapalı olabilir).");

    public static readonly Wrappers.Error AvailabilityOverlap =
        Wrappers.Error.Conflict("doctor.availability_overlap", "Bu aralık mevcut bir uygunluk kaydıyla çakışıyor.");

    public static readonly Wrappers.Error TimeOffNotFound =
        Wrappers.Error.NotFound("doctor.time_off_not_found", "İzin kaydı bulunamadı.");

    public static readonly Wrappers.Error TimeOffInPast =
        Wrappers.Error.Validation("doctor.time_off_in_past", "Geçmişte biten bir izin eklenemez.");

    public static readonly Wrappers.Error TimeOffOverlap =
        Wrappers.Error.Conflict("doctor.time_off_overlap", "Bu aralık mevcut bir izinle çakışıyor.");
}
