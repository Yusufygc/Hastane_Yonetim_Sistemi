using Hospital.Domain.Common;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Domain.Entities.Users;

/// <summary>
/// Doktor. Bir bölüme (dolayısıyla şubeye) bağlıdır; sunduğu hizmetleri (yönetici onaylı), haftalık uygunluk
/// pencerelerini ve izinlerini yönetir. Randevu alınabilirliği bu üç parçanın kesişiminden üretilir.
/// </summary>
public sealed class Doctor : User
{
    public const int NameMaxLength = 100;
    public const int SpecialtyMaxLength = 100;
    public const int DiplomaMaxLength = 50;
    public const int PhoneMaxLength = 32;

    private readonly List<DoctorServiceLink> _services = [];
    private readonly List<DoctorAvailability> _availabilities = [];
    private readonly List<DoctorTimeOff> _timeOffs = [];

    /// <summary>EF Core materialization için.</summary>
    private Doctor()
    {
        FirstName = null!;
        LastName = null!;
        Specialty = null!;
        DiplomaNo = null!;
    }

    private Doctor(
        Guid id,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        Guid departmentId,
        string specialty,
        string diplomaNo,
        string? phone,
        DateTimeOffset now)
        : base(id, email, passwordHash, UserRole.Doctor, now)
    {
        FirstName = firstName;
        LastName = lastName;
        DepartmentId = departmentId;
        Specialty = specialty;
        DiplomaNo = diplomaNo;
        Phone = phone;
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public Guid DepartmentId { get; private set; }

    public string Specialty { get; private set; }

    public string DiplomaNo { get; private set; }

    public string? Phone { get; private set; }

    public override string FullName => $"{FirstName} {LastName}";

    public IReadOnlyCollection<DoctorServiceLink> Services => _services;

    public IReadOnlyCollection<DoctorAvailability> Availabilities => _availabilities;

    public IReadOnlyCollection<DoctorTimeOff> TimeOffs => _timeOffs;

    public static Doctor Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        Guid departmentId,
        string specialty,
        string diplomaNo,
        string? phone,
        DateTimeOffset now) =>
        new(
            Guid.CreateVersion7(),
            email,
            passwordHash,
            Guard.NotBlank(firstName, "Ad", NameMaxLength),
            Guard.NotBlank(lastName, "Soyad", NameMaxLength),
            Guard.NotEmpty(departmentId, "Bölüm"),
            Guard.NotBlank(specialty, "Uzmanlık", SpecialtyMaxLength),
            Guard.NotBlank(diplomaNo, "Diploma numarası", DiplomaMaxLength),
            Guard.OptionalText(phone, "Telefon", PhoneMaxLength),
            now);

    /// <summary>Yönetici güncellemesi: kimlik bilgileri, bölüm, uzmanlık, diploma.</summary>
    public void UpdateByAdmin(string firstName, string lastName, Guid departmentId, string specialty, string diplomaNo, string? phone)
    {
        FirstName = Guard.NotBlank(firstName, "Ad", NameMaxLength);
        LastName = Guard.NotBlank(lastName, "Soyad", NameMaxLength);
        DepartmentId = Guard.NotEmpty(departmentId, "Bölüm");
        Specialty = Guard.NotBlank(specialty, "Uzmanlık", SpecialtyMaxLength);
        DiplomaNo = Guard.NotBlank(diplomaNo, "Diploma numarası", DiplomaMaxLength);
        Phone = Guard.OptionalText(phone, "Telefon", PhoneMaxLength);
    }

    /// <summary>Doktorun kendi profilinde değiştirebildikleri (ad/diploma/bölüm yöneticiye aittir).</summary>
    public void UpdateOwnProfile(string specialty, string? phone)
    {
        Specialty = Guard.NotBlank(specialty, "Uzmanlık", SpecialtyMaxLength);
        Phone = Guard.OptionalText(phone, "Telefon", PhoneMaxLength);
    }

    // ---------------------------------------------------------------- Hizmetler (talep → onay)

    public DoctorServiceLink? FindService(Guid serviceId) => _services.FirstOrDefault(s => s.ServiceId == serviceId);

    /// <summary>
    /// Doktor hizmet talep eder (<see cref="DoctorServiceStatus.Pending"/>). Reddedilmiş talep yeniden açılır;
    /// onaylı veya bekleyen talep için hata fırlatır.
    /// </summary>
    public DoctorServiceLink RequestService(Guid serviceId, Guid requestedBy, DateTimeOffset now)
    {
        var existing = FindService(serviceId);

        if (existing is null)
        {
            var link = new DoctorServiceLink(Id, Guard.NotEmpty(serviceId, "Hizmet"), requestedBy, now);
            _services.Add(link);
            return link;
        }

        if (existing.Status == DoctorServiceStatus.Rejected)
        {
            existing.Reopen(requestedBy, now);
            return existing;
        }

        throw new DomainException(existing.Status == DoctorServiceStatus.Approved
            ? "Bu hizmet zaten onaylı."
            : "Bu hizmet için talep zaten onay bekliyor.");
    }

    /// <summary>Yönetici doğrudan atar: onay adımı olmadan <see cref="DoctorServiceStatus.Approved"/>. İdempotenttir.</summary>
    public DoctorServiceLink AssignService(Guid serviceId, Guid adminId, DateTimeOffset now)
    {
        var existing = FindService(serviceId);

        if (existing is null)
        {
            existing = new DoctorServiceLink(Id, Guard.NotEmpty(serviceId, "Hizmet"), adminId, now);
            _services.Add(existing);
        }

        existing.Approve(adminId, now);
        return existing;
    }

    /// <summary>Bekleyen (veya daha önce reddedilmiş) talebi onaylar.</summary>
    public void ApproveService(Guid serviceId, Guid adminId, DateTimeOffset now)
    {
        var link = FindService(serviceId) ?? throw new DomainException("Doktorun bu hizmet için bir talebi yok.");
        link.Approve(adminId, now);
    }

    /// <summary>Bekleyen talebi reddeder.</summary>
    public void RejectService(Guid serviceId, Guid adminId, DateTimeOffset now)
    {
        var link = FindService(serviceId) ?? throw new DomainException("Doktorun bu hizmet için bir talebi yok.");
        link.Reject(adminId, now);
    }

    /// <summary>Hizmeti doktordan kaldırır (talebi geri çekme veya yöneticinin kaldırması). Kaldırıldıysa <c>true</c>.</summary>
    public bool RemoveService(Guid serviceId)
    {
        var link = FindService(serviceId);
        return link is not null && _services.Remove(link);
    }

    // ---------------------------------------------------------------- Uygunluk (haftalık pencereler)

    public DoctorAvailability? FindAvailability(Guid availabilityId) =>
        _availabilities.FirstOrDefault(a => a.Id == availabilityId);

    /// <summary>Verilen pencere ile çakışan başka bir uygunluk var mı? (<paramref name="ignoreId"/> güncellemede kendisini hariç tutar.)</summary>
    public DoctorAvailability? FindOverlappingAvailability(
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly validFrom,
        DateOnly? validTo,
        Guid? ignoreId = null) =>
        _availabilities.FirstOrDefault(a => a.Id != ignoreId && a.Overlaps(day, startTime, endTime, validFrom, validTo));

    public DoctorAvailability AddAvailability(
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly validFrom,
        DateOnly? validTo,
        IEnumerable<HospitalWorkingHours> hospitalHours)
    {
        EnsureAvailabilityRules(day, startTime, endTime, validFrom, validTo, hospitalHours, ignoreId: null);

        var availability = new DoctorAvailability(Id, day, startTime, endTime, validFrom, validTo);
        _availabilities.Add(availability);
        return availability;
    }

    public void UpdateAvailability(
        Guid availabilityId,
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly validFrom,
        DateOnly? validTo,
        IEnumerable<HospitalWorkingHours> hospitalHours)
    {
        var availability = FindAvailability(availabilityId) ?? throw new DomainException("Uygunluk kaydı bulunamadı.");

        EnsureAvailabilityRules(day, startTime, endTime, validFrom, validTo, hospitalHours, availabilityId);
        availability.Set(day, startTime, endTime, validFrom, validTo);
    }

    public bool RemoveAvailability(Guid availabilityId)
    {
        var availability = FindAvailability(availabilityId);
        return availability is not null && _availabilities.Remove(availability);
    }

    private void EnsureAvailabilityRules(
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly validFrom,
        DateOnly? validTo,
        IEnumerable<HospitalWorkingHours> hospitalHours,
        Guid? ignoreId)
    {
        if (endTime <= startTime)
        {
            throw new DomainException("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        }

        if (validTo is { } to && to < validFrom)
        {
            throw new DomainException("Geçerlilik bitişi başlangıçtan önce olamaz.");
        }

        if (!DoctorAvailability.FitsWithinHospitalHours(hospitalHours, day, startTime, endTime))
        {
            throw new DomainException("Uygunluk aralığı şubenin o günkü çalışma saatleri içinde olmalıdır (şube o gün kapalı olabilir).");
        }

        if (FindOverlappingAvailability(day, startTime, endTime, validFrom, validTo, ignoreId) is not null)
        {
            throw new DomainException("Bu aralık mevcut bir uygunluk kaydıyla çakışıyor.");
        }
    }

    // ---------------------------------------------------------------- İzin

    public DoctorTimeOff? FindTimeOff(Guid timeOffId) => _timeOffs.FirstOrDefault(t => t.Id == timeOffId);

    public DoctorTimeOff? FindOverlappingTimeOff(DateTimeOffset startAt, DateTimeOffset endAt) =>
        _timeOffs.FirstOrDefault(t => t.Overlaps(startAt, endAt));

    public DoctorTimeOff AddTimeOff(DateTimeOffset startAt, DateTimeOffset endAt, string? reason, DateTimeOffset now)
    {
        if (endAt <= now)
        {
            throw new DomainException("Geçmişte biten bir izin eklenemez.");
        }

        if (FindOverlappingTimeOff(startAt, endAt) is not null)
        {
            throw new DomainException("Bu aralık mevcut bir izinle çakışıyor.");
        }

        var timeOff = new DoctorTimeOff(Id, startAt, endAt, reason);
        _timeOffs.Add(timeOff);
        return timeOff;
    }

    public bool RemoveTimeOff(Guid timeOffId)
    {
        var timeOff = FindTimeOff(timeOffId);
        return timeOff is not null && _timeOffs.Remove(timeOff);
    }
}
