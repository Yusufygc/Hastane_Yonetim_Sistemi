using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Facility;

/// <summary>Bir şubenin haftanın bir günündeki çalışma aralığı. O gün için kayıt yoksa şube kapalıdır.</summary>
public sealed class HospitalWorkingHours : Entity
{
    /// <summary>EF Core materialization için.</summary>
    private HospitalWorkingHours()
    {
    }

    internal HospitalWorkingHours(Guid hospitalId, DayOfWeek day, TimeOnly openTime, TimeOnly closeTime)
        : base(Guid.CreateVersion7())
    {
        HospitalId = hospitalId;
        Day = day;
        Set(openTime, closeTime);
    }

    public Guid HospitalId { get; private set; }

    /// <summary>0 = Pazar … 6 = Cumartesi.</summary>
    public DayOfWeek Day { get; private set; }

    public TimeOnly OpenTime { get; private set; }

    public TimeOnly CloseTime { get; private set; }

    internal void Set(TimeOnly openTime, TimeOnly closeTime)
    {
        if (closeTime <= openTime)
        {
            throw new DomainException($"{Day}: kapanış saati açılış saatinden sonra olmalıdır.");
        }

        OpenTime = openTime;
        CloseTime = closeTime;
    }
}

/// <summary>Çalışma saati girdisi (komutlardan gelen ham değer).</summary>
public readonly record struct WorkingHoursInput(DayOfWeek Day, TimeOnly OpenTime, TimeOnly CloseTime);
