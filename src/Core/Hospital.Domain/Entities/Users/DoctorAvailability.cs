using Hospital.Domain.Common;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Domain.Entities.Users;

/// <summary>
/// Doktorun haftalık tekrar eden uygunluk penceresi (DOCTOR_AVAILABILITY): hangi gün hangi saat aralığında,
/// hangi tarihler arasında (<see cref="ValidFrom"/>..<see cref="ValidTo"/>, boş = süresiz) randevu alabilir.
/// Saatler şubenin saat diliminde yorumlanır; randevu yalnızca bu pencerelere (şube saatleri ile kesişen kısma) verilir.
/// </summary>
public sealed class DoctorAvailability : Entity
{
    /// <summary>EF Core materialization için.</summary>
    private DoctorAvailability()
    {
    }

    internal DoctorAvailability(
        Guid doctorId,
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly validFrom,
        DateOnly? validTo)
        : base(Guid.CreateVersion7())
    {
        DoctorId = doctorId;
        Set(day, startTime, endTime, validFrom, validTo);
    }

    public Guid DoctorId { get; private set; }

    /// <summary>0 = Pazar … 6 = Cumartesi.</summary>
    public DayOfWeek Day { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly? ValidTo { get; private set; }

    internal void Set(DayOfWeek day, TimeOnly startTime, TimeOnly endTime, DateOnly validFrom, DateOnly? validTo)
    {
        if (endTime <= startTime)
        {
            throw new DomainException("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        }

        if (validTo is { } to && to < validFrom)
        {
            throw new DomainException("Geçerlilik bitişi başlangıçtan önce olamaz.");
        }

        Day = day;
        StartTime = startTime;
        EndTime = endTime;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>Aynı gün, kesişen saat aralığı ve kesişen geçerlilik dönemi → çakışma.</summary>
    public bool Overlaps(DayOfWeek day, TimeOnly startTime, TimeOnly endTime, DateOnly validFrom, DateOnly? validTo) =>
        Day == day
        && StartTime < endTime && startTime < EndTime
        && ValidFrom <= (validTo ?? DateOnly.MaxValue) && validFrom <= (ValidTo ?? DateOnly.MaxValue);

    /// <summary>Pencere, şubenin o günkü çalışma saatleri içinde mi? Şube o gün kapalıysa <c>false</c>.</summary>
    public static bool FitsWithinHospitalHours(
        IEnumerable<HospitalWorkingHours> hospitalHours,
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        var hours = hospitalHours.FirstOrDefault(h => h.Day == day);
        return hours is not null && startTime >= hours.OpenTime && endTime <= hours.CloseTime;
    }
}
