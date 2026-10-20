using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Users;

/// <summary>Doktorun izin/yokluk aralığı (DOCTOR_TIME_OFF). Bu aralıkta slot üretilmez. Zamanlar UTC saklanır.</summary>
public sealed class DoctorTimeOff : Entity
{
    public const int ReasonMaxLength = 300;

    /// <summary>EF Core materialization için.</summary>
    private DoctorTimeOff()
    {
    }

    internal DoctorTimeOff(Guid doctorId, DateTimeOffset startAt, DateTimeOffset endAt, string? reason)
        : base(Guid.CreateVersion7())
    {
        if (endAt <= startAt)
        {
            throw new DomainException("İzin bitişi başlangıçtan sonra olmalıdır.");
        }

        DoctorId = doctorId;
        StartAt = startAt.ToUniversalTime();
        EndAt = endAt.ToUniversalTime();
        Reason = Guard.OptionalText(reason, "İzin nedeni", ReasonMaxLength);
    }

    public Guid DoctorId { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>Yarı açık aralık [StartAt, EndAt) kesişimi.</summary>
    public bool Overlaps(DateTimeOffset startAt, DateTimeOffset endAt) => StartAt < endAt && startAt < EndAt;
}
