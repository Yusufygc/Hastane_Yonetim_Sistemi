using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;

namespace Hospital.Domain.Scheduling;

/// <summary>Yarı açık zaman aralığı [Start, End), UTC.</summary>
public readonly record struct TimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;
}

/// <summary>Slot hesabı girdileri. Tüm zaman karşılaştırmaları UTC anlarıyla, haftalık kurallar şube saat diliminde yapılır.</summary>
/// <param name="Date">Şubenin yerel takvim günü.</param>
/// <param name="TimeZone">Şubenin saat dilimi.</param>
/// <param name="DurationMinutes">Hizmetin sabit süresi.</param>
/// <param name="GridMinutes">Slot başlangıç ızgarası (dakika); başlangıçlar gece yarısından itibaren bu adımın katlarındadır.</param>
/// <param name="Now">Şu an (UTC).</param>
/// <param name="MinLead">En az hazırlık süresi: bundan yakın başlangıçlar elenir.</param>
/// <param name="HorizonDays">Bugünden itibaren en fazla kaç gün ilerisine randevu alınabilir.</param>
/// <param name="WorkingHours">Şubenin haftalık çalışma saatleri (kayıt yoksa o gün kapalı).</param>
/// <param name="Availabilities">Doktorun haftalık uygunluk pencereleri.</param>
/// <param name="TimeOffs">Doktorun izin aralıkları (UTC).</param>
/// <param name="Booked">Doktorun slotu tutan (PENDING/APPROVED) randevuları (UTC).</param>
public sealed record SlotParameters(
    DateOnly Date,
    TimeZoneInfo TimeZone,
    int DurationMinutes,
    int GridMinutes,
    DateTimeOffset Now,
    TimeSpan MinLead,
    int HorizonDays,
    IReadOnlyCollection<HospitalWorkingHours> WorkingHours,
    IReadOnlyCollection<DoctorAvailability> Availabilities,
    IReadOnlyCollection<TimeRange> TimeOffs,
    IReadOnlyCollection<TimeRange> Booked);

/// <summary>
/// Sabit süreli slot üretimi (saf fonksiyon, I/O yok):
/// uygunluk pencereleri (gün + geçerlilik) ∩ şube çalışma saatleri → ızgarada adaylar (tamamen sığmalı)
/// → izin ve dolu randevularla çakışanlar elenir → geçmiş / minimum hazırlık / ileri tarih sınırı uygulanır.
/// Yerel saat → UTC çevirisi DST'ye duyarlıdır; var olmayan yerel saatler (ileri geçiş boşluğu) atlanır.
/// </summary>
public static class SlotCalculator
{
    public static IReadOnlyList<TimeRange> Calculate(SlotParameters p)
    {
        if (p.DurationMinutes <= 0 || p.GridMinutes <= 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(p.Now, p.TimeZone).DateTime);
        if (p.Date < today || p.Date > today.AddDays(p.HorizonDays))
        {
            return [];
        }

        var hours = p.WorkingHours.FirstOrDefault(h => h.Day == p.Date.DayOfWeek);
        if (hours is null)
        {
            return [];
        }

        var earliestStart = p.Now + p.MinLead;
        var duration = TimeSpan.FromMinutes(p.DurationMinutes);
        var slots = new SortedSet<TimeRange>(Comparer<TimeRange>.Create((a, b) => a.Start.CompareTo(b.Start)));

        var windows = p.Availabilities.Where(a =>
            a.Day == p.Date.DayOfWeek && a.ValidFrom <= p.Date && (a.ValidTo is null || p.Date <= a.ValidTo));

        foreach (var window in windows)
        {
            var open = TimeOnly.FromTimeSpan(Max(window.StartTime.ToTimeSpan(), hours.OpenTime.ToTimeSpan()));
            var close = TimeOnly.FromTimeSpan(Min(window.EndTime.ToTimeSpan(), hours.CloseTime.ToTimeSpan()));
            if (close <= open)
            {
                continue;
            }

            var firstMinute = (int)Math.Ceiling(open.ToTimeSpan().TotalMinutes / p.GridMinutes) * p.GridMinutes;
            var lastMinute = (int)Math.Floor(close.ToTimeSpan().TotalMinutes);

            for (var minute = firstMinute; minute + p.DurationMinutes <= lastMinute; minute += p.GridMinutes)
            {
                var local = p.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(minute);
                if (p.TimeZone.IsInvalidTime(local))
                {
                    continue;
                }

                var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, p.TimeZone), TimeSpan.Zero);
                var candidate = new TimeRange(start, start + duration);

                if (candidate.Start < earliestStart
                    || p.TimeOffs.Any(candidate.Overlaps)
                    || p.Booked.Any(candidate.Overlaps))
                {
                    continue;
                }

                slots.Add(candidate);
            }
        }

        return [.. slots];
    }

    /// <summary>Şubenin yerel gün sınırları (UTC): [gün başı, ertesi gün başı). DST günlerinde 23 veya 25 saat olabilir.</summary>
    public static TimeRange LocalDayBounds(DateOnly date, TimeZoneInfo timeZone)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var next = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

        return new TimeRange(ToUtc(start, timeZone), ToUtc(next, timeZone));
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        // Gece yarısı DST boşluğuna denk gelirse (bazı bölgelerde) boşluğun sonuna ilerle.
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a <= b ? a : b;
}
