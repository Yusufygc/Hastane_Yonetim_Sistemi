using System.Collections.Concurrent;
using AwesomeAssertions;
using Hospital.Application.Exceptions;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// Çakışma koruması veritabanı seviyesinde (PostgreSQL exclusion constraint) çalışıyor mu? (Faz 1B spike'ı; Faz 4'te
/// doktor/hizmet/şube FK'li gerçek modele uyarlandı.) Her test kendi doktorunu açar; testler birbirini etkilemez.
/// HTTP üzerinden gerçek yarış testi: <see cref="AppointmentConcurrencyTests"/>.
/// </summary>
public class AppointmentOverlapConstraintTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string DoctorConstraint = "ex_doctor_no_overlap";
    private const string PatientConstraint = "ex_patient_no_overlap";

    private static readonly DateTimeOffset Day = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute) => Day.AddHours(hour).AddMinutes(minute);

    [Fact]
    public async Task Ayni_doktor_ayni_aralik_cakisir()
    {
        var doctor = await NewDoctorAsync();
        var (p1, p2) = (await NewPatientAsync(), await NewPatientAsync());
        await BookAsync(p1, doctor, At(10, 0), At(10, 30));

        var act = () => BookAsync(p2, doctor, At(10, 0), At(10, 30));

        (await act.Should().ThrowAsync<DataConflictException>()).Which.ConstraintName.Should().Be(DoctorConstraint);
    }

    [Theory]
    [InlineData(9, 45, 10, 15)]   // başı çakışır
    [InlineData(10, 15, 10, 45)]  // sonu çakışır
    [InlineData(10, 5, 10, 25)]   // tamamen içinde
    [InlineData(9, 30, 11, 0)]    // kapsar
    public async Task Ayni_doktorda_kismi_kesisim_cakisir(int sh, int sm, int eh, int em)
    {
        var doctor = await NewDoctorAsync();
        var (p1, p2) = (await NewPatientAsync(), await NewPatientAsync());
        await BookAsync(p1, doctor, At(10, 0), At(10, 30));

        var act = () => BookAsync(p2, doctor, At(sh, sm), At(eh, em));

        (await act.Should().ThrowAsync<DataConflictException>()).Which.ConstraintName.Should().Be(DoctorConstraint);
    }

    [Fact]
    public async Task Bitisik_araliklar_cakismaz()
    {
        var doctor = await NewDoctorAsync();
        var (p1, p2, p3) = (await NewPatientAsync(), await NewPatientAsync(), await NewPatientAsync());

        await BookAsync(p1, doctor, At(10, 0), At(10, 30));
        await BookAsync(p2, doctor, At(10, 30), At(11, 0)); // başlangıç = önceki bitiş
        await BookAsync(p3, doctor, At(9, 30), At(10, 0));  // bitiş = sonraki başlangıç

        (await CountAsync(doctor)).Should().Be(3);
    }

    [Fact]
    public async Task Farkli_doktor_ayni_saatte_cakismaz()
    {
        var (d1, d2) = (await NewDoctorAsync(), await NewDoctorAsync());
        var (p1, p2) = (await NewPatientAsync(), await NewPatientAsync());

        await BookAsync(p1, d1, At(10, 0), At(10, 30));
        await BookAsync(p2, d2, At(10, 0), At(10, 30));

        (await CountAsync(d1)).Should().Be(1);
        (await CountAsync(d2)).Should().Be(1);
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("REJECTED")]
    [InlineData("COMPLETED")]
    public async Task Iptal_red_ve_tamamlanan_randevu_slotu_serbest_birakir(string status)
    {
        var doctor = await NewDoctorAsync();
        var (p1, p2) = (await NewPatientAsync(), await NewPatientAsync());
        var first = await BookAsync(p1, doctor, At(10, 0), At(10, 30));
        await SetStatusAsync(first, status);

        await BookAsync(p2, doctor, At(10, 0), At(10, 30));

        (await CountAsync(doctor)).Should().Be(2);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("APPROVED")]
    public async Task Bekleyen_ve_onayli_randevu_slotu_tutar(string status)
    {
        var doctor = await NewDoctorAsync();
        var (p1, p2) = (await NewPatientAsync(), await NewPatientAsync());
        var first = await BookAsync(p1, doctor, At(10, 0), At(10, 30));
        await SetStatusAsync(first, status);

        var act = () => BookAsync(p2, doctor, At(10, 0), At(10, 30));

        await act.Should().ThrowAsync<DataConflictException>();
    }

    [Fact]
    public async Task Iptal_edilen_randevunun_slotu_yeniden_alinabilir_ve_yeni_randevu_yine_korunur()
    {
        var doctor = await NewDoctorAsync();
        var (p1, p2, p3) = (await NewPatientAsync(), await NewPatientAsync(), await NewPatientAsync());
        var first = await BookAsync(p1, doctor, At(10, 0), At(10, 30));
        await SetStatusAsync(first, "CANCELLED");
        await BookAsync(p2, doctor, At(10, 0), At(10, 30));

        var act = () => BookAsync(p3, doctor, At(10, 0), At(10, 30));

        await act.Should().ThrowAsync<DataConflictException>();
    }

    [Fact]
    public async Task Ayni_hasta_iki_doktorda_ayni_saatte_randevu_alamaz()
    {
        var (d1, d2) = (await NewDoctorAsync(), await NewDoctorAsync());
        var patient = await NewPatientAsync();
        await BookAsync(patient, d1, At(10, 0), At(10, 30));

        var act = () => BookAsync(patient, d2, At(10, 15), At(10, 45));

        (await act.Should().ThrowAsync<DataConflictException>()).Which.ConstraintName.Should().Be(PatientConstraint);
    }

    [Fact]
    public async Task Ayni_hasta_farkli_saatlerde_randevu_alabilir()
    {
        var (d1, d2) = (await NewDoctorAsync(), await NewDoctorAsync());
        var patient = await NewPatientAsync();

        await BookAsync(patient, d1, At(10, 0), At(10, 30));
        await BookAsync(patient, d2, At(10, 30), At(11, 0));

        (await CountAsync(d1)).Should().Be(1);
        (await CountAsync(d2)).Should().Be(1);
    }

    [Fact]
    public async Task Yirmi_paralel_istek_ayni_slota_yalnizca_biri_basarili_olur()
    {
        const int parallelism = 20;
        var doctor = await NewDoctorAsync();
        var patients = new List<Guid>();
        for (var i = 0; i < parallelism; i++)
        {
            patients.Add(await NewPatientAsync());
        }

        var gate = new TaskCompletionSource();

        var tasks = patients.Select(patientId => Task.Run(async () =>
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Appointments.Add(NewRequest(patientId, doctor, At(14, 0), At(14, 30)));

            await gate.Task; // hepsi hazır olunca aynı anda kaydet

            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (DataConflictException ex) when (ex.ConstraintName == DoctorConstraint)
            {
                return false;
            }
        })).ToList();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(succeeded => succeeded).Should().Be(1);
        results.Count(succeeded => !succeeded).Should().Be(parallelism - 1);
        (await CountAsync(doctor)).Should().Be(1);
    }

    [Fact]
    public async Task Bitis_baslangictan_once_olan_aralik_veritabaninda_da_reddedilir()
    {
        // Domain bunu zaten engeller; CHECK kısıtı ham SQL ile atlatılmaya karşı ikinci savunmadır.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var patient = await NewPatientAsync();
        var doctor = await NewDoctorAsync();
        var context = Contexts[doctor];

        var act = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO appointment (id, hospital_id, patient_id, doctor_id, service_id, start_at, end_at, status, " +
            "service_name_snapshot, duration_minutes_snapshot, price_snapshot, currency_snapshot, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, 'PENDING', 'Hizmet', 30, 100, 'TRY', now(), now())",
            Guid.NewGuid(), context.HospitalId, patient, doctor, context.Service.ServiceId, At(10, 30), At(10, 0));

        await act.Should().ThrowAsync<Exception>().WithMessage("*ck_appointment_range*");
    }

    private async Task<Guid> NewPatientAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var account = TestAccount.New();
        var patient = Patient.Register(
            account.Email, "test-hash", "Ayşe", "Yılmaz", account.IdentityNumber, new DateOnly(1990, 5, 17), null, DateTimeOffset.UtcNow);

        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    private sealed record DoctorContext(Guid HospitalId, ServiceSnapshot Service);

    private static readonly ConcurrentDictionary<Guid, DoctorContext> Contexts = new();

    /// <summary>Seed şubesinde gerçek bir bölüm + hizmet + doktor yazar (FK'ler gerçek kayıtlara işaret eder).</summary>
    private async Task<Guid> NewDoctorAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hospital = await db.Hospitals.AsNoTracking().OrderBy(h => h.Id).FirstAsync();
        var department = Department.Create(hospital.Id, $"Bölüm {Guid.NewGuid():N}");
        var service = Service.Create(hospital.Id, department.Id, $"Hizmet {Guid.NewGuid():N}", 30, 100m, "TRY");
        var unique = Guid.NewGuid().ToString("N");
        var doctor = Doctor.Create(
            $"doktor.{unique}@test.local", "test-hash", "Mehmet", "Demir", department.Id, "Dahiliye", $"D-{unique}", null, DateTimeOffset.UtcNow);

        db.Departments.Add(department);
        db.Services.Add(service);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        Contexts[doctor.Id] = new DoctorContext(hospital.Id, ServiceSnapshot.From(service));
        return doctor.Id;
    }

    private static Appointment NewRequest(Guid patientId, Guid doctorId, DateTimeOffset start, DateTimeOffset end)
    {
        var context = Contexts[doctorId];
        return Appointment.Request(patientId, doctorId, context.HospitalId, context.Service, start, end, null, DateTimeOffset.UtcNow);
    }

    private async Task<Guid> BookAsync(Guid patientId, Guid doctorId, DateTimeOffset start, DateTimeOffset end)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var appointment = NewRequest(patientId, doctorId, start, end);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    /// <summary>Kısıt davranışı durum değerine bağlıdır; durum doğrudan SQL ile değiştirilir (geçiş kuralları ayrı sınanır).</summary>
    private async Task SetStatusAsync(Guid appointmentId, string status)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("UPDATE appointment SET status = {0} WHERE id = {1}", status, appointmentId);
    }

    private async Task<int> CountAsync(Guid doctorId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Appointments.AsNoTracking().CountAsync(a => a.DoctorId == doctorId);
    }
}
