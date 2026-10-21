using AwesomeAssertions;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Hospital.Domain.Scheduling;

namespace Hospital.UnitTests.Domain;

public class SlotCalculatorTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly DateTimeOffset Now = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly TodayIstanbul = new(2026, 10, 20);

    private static HospitalBranch NewHospital(params WorkingHoursInput[] hours)
    {
        var hospital = HospitalBranch.Create(Guid.NewGuid(), "Merkez", null, null, "Europe/Istanbul");
        hospital.SetWorkingHours(hours.Length > 0 ? hours : DefaultHours());
        return hospital;
    }

    private static WorkingHoursInput[] DefaultHours() =>
    [
        new(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
        new(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
        new(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
    ];

    private static Doctor DoctorWith(HospitalBranch hospital, params (DayOfWeek Day, TimeOnly Start, TimeOnly End, DateOnly From, DateOnly? To)[] windows)
    {
        var doctor = Doctor.Create("d@e.test", "hash", "Ayşe", "Yılmaz", Guid.NewGuid(), "Dahiliye", "D-1", null, Now);
        foreach (var (day, start, end, from, to) in windows)
        {
            doctor.AddAvailability(day, start, end, from, to, hospital.WorkingHours);
        }

        return doctor;
    }

    private static Doctor MondayDoctor(HospitalBranch hospital, int fromHour = 9, int toHour = 12) =>
        DoctorWith(hospital, (DayOfWeek.Monday, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0), new DateOnly(2026, 1, 1), null));

    private static SlotParameters Params(
        HospitalBranch hospital,
        Doctor doctor,
        DateOnly date,
        int duration = 30,
        int grid = 15,
        DateTimeOffset? now = null,
        TimeSpan? minLead = null,
        TimeZoneInfo? zone = null,
        IReadOnlyCollection<TimeRange>? timeOffs = null,
        IReadOnlyCollection<TimeRange>? booked = null) =>
        new(
            date,
            zone ?? Istanbul,
            duration,
            grid,
            now ?? Now,
            minLead ?? TimeSpan.FromMinutes(60),
            60,
            hospital.WorkingHours,
            doctor.Availabilities,
            timeOffs ?? [],
            booked ?? []);

    private static TimeOnly LocalStart(TimeRange slot, TimeZoneInfo zone) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(slot.Start, zone).DateTime);

    private static List<TimeOnly> Starts(IReadOnlyList<TimeRange> slots, TimeZoneInfo? zone = null) =>
        slots.Select(s => LocalStart(s, zone ?? Istanbul)).ToList();

    private static TimeRange Range(DateOnly date, int sh, int sm, int eh, int em, TimeZoneInfo? zone = null)
    {
        var tz = zone ?? Istanbul;
        DateTimeOffset At(int h, int m) =>
            new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(h, m)), tz), TimeSpan.Zero);

        return new TimeRange(At(sh, sm), At(eh, em));
    }

    // ---------- Temel üretim ----------

    [Fact]
    public void Pencere_icinde_izgarada_slotlar_uretilir_son_slot_pencereye_tamamen_sigar()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital); // 09:00-12:00

        var slots = SlotCalculator.Calculate(Params(hospital, doctor, Monday, duration: 30, grid: 15));

        var starts = Starts(slots);
        starts.First().Should().Be(new TimeOnly(9, 0));
        starts.Last().Should().Be(new TimeOnly(11, 30)); // 11:30-12:00 sığar; 11:45-12:15 sığmaz
        starts.Should().HaveCount(11);
        slots.Should().OnlyContain(s => s.End - s.Start == TimeSpan.FromMinutes(30));
        slots.Select(s => s.Start).Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData(15, 12)]  // 09:00 … 11:45
    [InlineData(30, 11)]  // 09:00 … 11:30 (15 dk ızgara)
    [InlineData(60, 9)]   // 09:00 … 11:00
    public void Slot_sayisi_hizmet_suresine_gore_degisir(int duration, int expected)
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);

        var slots = SlotCalculator.Calculate(Params(hospital, doctor, Monday, duration: duration, grid: 15));

        slots.Should().HaveCount(expected);
    }

    [Fact]
    public void Baslangiclar_gece_yarisindan_itibaren_izgara_adiminin_katlaridir()
    {
        var hospital = NewHospital();
        var doctor = DoctorWith(hospital, (DayOfWeek.Monday, new TimeOnly(9, 10), new TimeOnly(11, 0), new DateOnly(2026, 1, 1), null));

        var starts = Starts(SlotCalculator.Calculate(Params(hospital, doctor, Monday, duration: 30, grid: 15)));

        starts.First().Should().Be(new TimeOnly(9, 15)); // 09:10 → bir sonraki ızgara noktası
        starts.Should().OnlyContain(t => t.Minute % 15 == 0);
    }

    [Fact]
    public void Sure_pencereden_uzunsa_slot_uretilmez()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital, 9, 10);

        SlotCalculator.Calculate(Params(hospital, doctor, Monday, duration: 90)).Should().BeEmpty();
    }

    // ---------- Şube çalışma saatleri ile kesişim ----------

    [Fact]
    public void Uygunluk_sube_saatlerinden_genisse_kesisim_alinir()
    {
        // Doktor penceresi 08:00-17:00 ama şube Pazartesi 10:00-12:00 açık.
        var hospital = NewHospital(new WorkingHoursInput(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)));
        var doctor = MondayDoctor(hospital, 8, 17);
        hospital.SetWorkingHours([new WorkingHoursInput(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(12, 0))]);

        var starts = Starts(SlotCalculator.Calculate(Params(hospital, doctor, Monday, duration: 30)));

        starts.First().Should().Be(new TimeOnly(10, 0));
        starts.Last().Should().Be(new TimeOnly(11, 30));
    }

    [Fact]
    public void Sube_kapaliysa_uygunluk_olsa_bile_slot_yoktur()
    {
        var hospital = NewHospital(new WorkingHoursInput(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)));
        var doctor = MondayDoctor(hospital);
        hospital.SetWorkingHours([]); // tüm günler kapalı

        SlotCalculator.Calculate(Params(hospital, doctor, Monday)).Should().BeEmpty();
    }

    [Fact]
    public void Uygunlugu_olmayan_gun_bos_doner()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);

        SlotCalculator.Calculate(Params(hospital, doctor, Monday.AddDays(1))).Should().BeEmpty(); // Salı
    }

    // ---------- Geçerlilik dönemi ----------

    [Theory]
    [InlineData("2026-11-03", null, false)]   // pencere ertesi günden başlıyor
    [InlineData("2026-10-01", "2026-11-01", false)] // dönem Monday'den önce bitti
    [InlineData("2026-10-01", "2026-11-02", true)]  // bitiş günü dahil
    [InlineData("2026-11-02", null, true)]          // başlangıç günü dahil
    public void Gecerlilik_donemi_disindaki_gun_slot_uretmez(string from, string? to, bool hasSlots)
    {
        var hospital = NewHospital();
        var doctor = DoctorWith(hospital, (
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(12, 0),
            DateOnly.Parse(from),
            to is null ? null : DateOnly.Parse(to)));

        var slots = SlotCalculator.Calculate(Params(hospital, doctor, Monday));

        (slots.Count > 0).Should().Be(hasSlots);
    }

    // ---------- İzin ve dolu randevular ----------

    [Fact]
    public void Izinle_kesisen_slotlar_elenir()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);
        var timeOff = Range(Monday, 10, 0, 11, 0);

        var starts = Starts(SlotCalculator.Calculate(Params(hospital, doctor, Monday, timeOffs: [timeOff])));

        // 09:45-10:15 izne giriyor → elenir; 09:30-10:00 ve 11:00-11:30 serbest (yarı açık aralık).
        starts.Should().Contain(new TimeOnly(9, 30)).And.Contain(new TimeOnly(11, 0));
        starts.Should().NotContain([new TimeOnly(9, 45), new TimeOnly(10, 0), new TimeOnly(10, 30), new TimeOnly(10, 45)]);
    }

    [Fact]
    public void Tum_gunu_kapsayan_izin_slotu_sifirlar()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);
        var timeOff = Range(Monday, 0, 0, 23, 59);

        SlotCalculator.Calculate(Params(hospital, doctor, Monday, timeOffs: [timeOff])).Should().BeEmpty();
    }

    [Fact]
    public void Dolu_randevuyla_kesisen_slotlar_elenir_bitisikler_kalir()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);
        var booked = Range(Monday, 10, 0, 10, 30);

        var starts = Starts(SlotCalculator.Calculate(Params(hospital, doctor, Monday, booked: [booked])));

        starts.Should().NotContain([new TimeOnly(9, 45), new TimeOnly(10, 0), new TimeOnly(10, 15)]);
        starts.Should().Contain([new TimeOnly(9, 30), new TimeOnly(10, 30)]); // bitişik: çakışmaz
        starts.Should().HaveCount(11 - 3);
    }

    // ---------- Zaman kuralları ----------

    [Fact]
    public void Minimum_hazirlik_suresinden_yakin_slotlar_elenir()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);
        // Yerel 08:30 (UTC 05:30) + 60 dk hazırlık → 09:30'dan önceki başlangıçlar elenir.
        var now = Range(Monday, 8, 30, 8, 31).Start;

        var starts = Starts(SlotCalculator.Calculate(Params(hospital, doctor, Monday, now: now)));

        starts.First().Should().Be(new TimeOnly(9, 30));
    }

    [Fact]
    public void Gecmis_gun_ve_ileri_tarih_siniri_bos_doner()
    {
        var hospital = NewHospital();
        var doctor = DoctorWith(hospital,
            (DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), new DateOnly(2026, 1, 1), null));
        var pastMonday = new DateOnly(2026, 10, 19);       // "bugün" Salı 2026-10-20
        var lastMondayInHorizon = new DateOnly(2026, 12, 14);  // bugün + 55 gün
        var beyondHorizon = new DateOnly(2026, 12, 21);        // bugün + 62 gün

        SlotCalculator.Calculate(Params(hospital, doctor, pastMonday)).Should().BeEmpty();
        SlotCalculator.Calculate(Params(hospital, doctor, lastMondayInHorizon)).Should().NotBeEmpty();
        SlotCalculator.Calculate(Params(hospital, doctor, beyondHorizon)).Should().BeEmpty();
    }

    [Fact]
    public void Bugun_icin_gecmis_saatler_elenir()
    {
        var hospital = NewHospital();
        var doctor = DoctorWith(hospital,
            (DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0), new DateOnly(2026, 1, 1), null));
        // Salı 2026-10-20 yerel 10:10; hazırlık 60 dk → ilk slot 11:15 (ızgara).
        var now = Range(TodayIstanbul, 10, 10, 10, 11).Start;

        var starts = Starts(SlotCalculator.Calculate(Params(hospital, doctor, TodayIstanbul, now: now)));

        starts.First().Should().Be(new TimeOnly(11, 15));
        starts.Last().Should().Be(new TimeOnly(11, 30));
    }

    [Fact]
    public void Gecersiz_sure_veya_izgara_bos_doner()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);

        SlotCalculator.Calculate(Params(hospital, doctor, Monday, duration: 0)).Should().BeEmpty();
        SlotCalculator.Calculate(Params(hospital, doctor, Monday, grid: 0)).Should().BeEmpty();
    }

    // ---------- Saat dilimi / DST ----------

    [Fact]
    public void Slotlar_UTC_olarak_sube_saat_diliminden_cevrilir()
    {
        var hospital = NewHospital();
        var doctor = MondayDoctor(hospital);

        var slots = SlotCalculator.Calculate(Params(hospital, doctor, Monday));

        // Yerel 09:00 Europe/Istanbul (UTC+3) = 06:00 UTC.
        slots.First().Start.Should().Be(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero));
        slots.First().Start.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Ileri_DST_gecisinde_var_olmayan_yerel_saatler_atlanir()
    {
        // Avrupa/Berlin 2027-03-28: 02:00 → 03:00 (02:00-02:59 yerel saat yok).
        var gapDay = new DateOnly(2027, 3, 28); // Pazar
        var hospital = HospitalBranch.Create(Guid.NewGuid(), "Berlin", null, null, "Europe/Berlin");
        hospital.SetWorkingHours([new WorkingHoursInput(DayOfWeek.Sunday, new TimeOnly(0, 0), new TimeOnly(23, 0))]);
        var doctor = DoctorWith(hospital, (DayOfWeek.Sunday, new TimeOnly(1, 0), new TimeOnly(5, 0), new DateOnly(2027, 1, 1), null));
        var now = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var slots = SlotCalculator.Calculate(Params(hospital, doctor, gapDay, now: now, zone: Berlin));

        var starts = Starts(slots, Berlin);
        starts.Should().NotContain(t => t.Hour == 2);
        starts.Should().Contain(new TimeOnly(1, 45)).And.Contain(new TimeOnly(3, 0));
        slots.Select(s => s.Start).Should().OnlyHaveUniqueItems();
        slots.Select(s => s.Start).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Gun_sinirlari_DST_gunlerinde_23_ve_25_saattir()
    {
        SlotCalculator.LocalDayBounds(new DateOnly(2027, 3, 28), Berlin).End
            .Subtract(SlotCalculator.LocalDayBounds(new DateOnly(2027, 3, 28), Berlin).Start)
            .Should().Be(TimeSpan.FromHours(23));

        SlotCalculator.LocalDayBounds(new DateOnly(2027, 10, 31), Berlin).End
            .Subtract(SlotCalculator.LocalDayBounds(new DateOnly(2027, 10, 31), Berlin).Start)
            .Should().Be(TimeSpan.FromHours(25));

        SlotCalculator.LocalDayBounds(new DateOnly(2026, 11, 2), Istanbul).End
            .Subtract(SlotCalculator.LocalDayBounds(new DateOnly(2026, 11, 2), Istanbul).Start)
            .Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void TimeRange_yari_acik_aralikta_bitisik_cakismaz()
    {
        var a = new TimeRange(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 2, 10, 30, 0, TimeSpan.Zero));
        var adjacent = new TimeRange(a.End, a.End.AddMinutes(30));
        var overlapping = new TimeRange(a.Start.AddMinutes(15), a.End.AddMinutes(15));

        a.Overlaps(adjacent).Should().BeFalse();
        adjacent.Overlaps(a).Should().BeFalse();
        a.Overlaps(overlapping).Should().BeTrue();
        overlapping.Overlaps(a).Should().BeTrue();
    }
}
