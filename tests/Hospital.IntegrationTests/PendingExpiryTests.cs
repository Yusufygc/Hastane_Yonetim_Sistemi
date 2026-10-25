using System.Net;
using AwesomeAssertions;
using Hospital.Application.Features.Appointments;
using Hospital.Domain.Entities.Appointments;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// Doktor yanıt vermeyen bekleyen talepler zaman aşımıyla otomatik reddedilir (slot serbest kalır, hastaya e-posta gider).
/// Varsayılan süre 48 saattir; testler talebi geriye taşıyarak süreyi doldurur (gerçek saat/JWT bozulmaz).
/// </summary>
public class PendingExpiryTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly TimeSpan Overdue = TimeSpan.FromHours(49);

    private async Task BackdateAsync(Guid appointmentId, TimeSpan by)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE appointment SET created_at = created_at - {0} WHERE id = {1}", by, appointmentId);
    }

    /// <summary>Süresi dolmuş tüm talepleri, arka plan işleyicinin yaptığı gibi randevu başına ayrı kapsamla düşürür.</summary>
    private async Task<int> RunExpiryAsync()
    {
        IReadOnlyList<Guid> due;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<PendingAppointmentExpirer>().FindDueAsync(CancellationToken.None);
        }

        var expired = 0;
        foreach (var id in due)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<PendingAppointmentExpirer>().ExpireAsync(id, CancellationToken.None))
            {
                expired++;
            }
        }

        return expired;
    }

    private async Task<(Guid? ChangedBy, string Status)> RawStateAsync(Guid appointmentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointmentId);
        return (row.StatusChangedBy, AppointmentStatusNames.From(row.Status));
    }

    [Fact]
    public async Task Suresi_dolan_bekleyen_talep_reddedilir_neden_sistem_islemi_slot_serbest_kalir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup, 10, 0);
        await BackdateAsync(id, Overdue);

        var expired = await RunExpiryAsync();

        expired.Should().BeGreaterThanOrEqualTo(1);
        var appointment = await ApiClient.ReadJsonAsync(await patient.GetAsync($"/api/appointments/{id}"));
        appointment.GetProperty("status").GetString().Should().Be("REJECTED");
        appointment.GetProperty("decisionReason").GetString().Should().Be(Appointment.ExpiredReason);
        (await RawStateAsync(id)).ChangedBy.Should().BeNull(); // işlemi yapan kullanıcı yok (sistem)

        // Slot serbest: başka hasta aynı saati alabilir.
        var other = await AppointmentKit.NewPatientAsync(fixture);
        await AppointmentKit.BookOkAsync(other, setup, 10, 0);
    }

    [Fact]
    public async Task Hastaya_neden_ile_red_e_postasi_kuyruga_girer_dogru_tutarda_tek_kez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var patientEmail = await OutboxKit.EmailOfAsync(patient);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        await BackdateAsync(id, Overdue);

        await RunExpiryAsync();
        await RunExpiryAsync(); // ikinci tur: zaten düşmüş, yeni e-posta yok

        var rejections = (await OutboxKit.ToAsync(fixture.Factory, patientEmail)).Where(m => m.TemplateKey == "appointment.rejected.patient").ToList();
        var mail = rejections.Should().ContainSingle().Subject;
        mail.Body.Should().Contain(Appointment.ExpiredReason).And.Contain("onaylanmadı");
        (await OutboxKit.ToAsync(fixture.Factory, setup.DoctorEmail)).Select(m => m.TemplateKey).Should().NotContain("appointment.rejected.patient");
    }

    [Fact]
    public async Task Suresi_dolmayan_onayli_ve_baska_durumdaki_randevulara_dokunulmaz()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (p1, p2, p3, p4) = (
            await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture),
            await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var fresh = await AppointmentKit.BookOkAsync(p1, setup, 9, 0);          // yeni bekleyen → dokunulmaz
        var approved = await AppointmentKit.BookOkAsync(p2, setup, 9, 30);      // onaylı (eski) → dokunulmaz
        var cancelled = await AppointmentKit.BookOkAsync(p3, setup, 10, 0);     // iptal (eski) → dokunulmaz
        var stale = await AppointmentKit.BookOkAsync(p4, setup, 10, 30);        // eski bekleyen → düşer
        await setup.Doctor.PostAsync($"/api/appointments/{approved}/approve");
        await p3.PostAsync($"/api/appointments/{cancelled}/cancel", new { Reason = "Vazgeçtim" });
        foreach (var id in new[] { approved, cancelled, stale })
        {
            await BackdateAsync(id, Overdue);
        }

        await RunExpiryAsync();

        (await RawStateAsync(fresh)).Status.Should().Be("PENDING");
        (await RawStateAsync(approved)).Status.Should().Be("APPROVED");
        (await RawStateAsync(cancelled)).Status.Should().Be("CANCELLED");
        (await RawStateAsync(stale)).Status.Should().Be("REJECTED");
    }

    [Fact]
    public async Task Baslangici_gecmis_bekleyen_talep_suresi_dolmasa_da_dusurulur()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        await AppointmentKit.MoveToPastAsync(fixture, id); // başlangıç geçmişte, talep yeni

        await RunExpiryAsync();

        (await RawStateAsync(id)).Status.Should().Be("REJECTED");
    }

    [Fact]
    public async Task Suresi_dolmamis_talep_ExpireAsync_ile_dogrudan_cagrilsa_da_dusurulmez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var expired = await scope.ServiceProvider.GetRequiredService<PendingAppointmentExpirer>().ExpireAsync(id, CancellationToken.None);

        expired.Should().BeFalse();
        (await RawStateAsync(id)).Status.Should().Be("PENDING");
    }

    [Fact]
    public async Task Doktor_ayni_anda_onaylarsa_onay_gecerli_kalir_zaman_asimi_atlanir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        await BackdateAsync(id, Overdue);

        // Doktor onayı ile zaman aşımı yarışı: ikisi aynı anda başlar.
        var gate = new TaskCompletionSource();
        var approve = Task.Run(async () =>
        {
            await gate.Task;
            return await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");
        });
        var expire = Task.Run(async () =>
        {
            await gate.Task;
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<PendingAppointmentExpirer>().ExpireAsync(id, CancellationToken.None);
        });
        gate.SetResult();
        var approveResponse = await approve;
        var expired = await expire;

        // Yarışı kim kazanırsa: sonuç tutarlıdır — ya onaylı ya da otomatik reddedilmiş; ikisi birden olmaz.
        var status = (await RawStateAsync(id)).Status;
        status.Should().BeOneOf("APPROVED", "REJECTED");
        if (status == "APPROVED")
        {
            approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
            expired.Should().BeFalse();
        }
        else
        {
            expired.Should().BeTrue();
            approveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Arka_plan_isleyici_suresi_dolan_talebi_kendiliginden_dusurur()
    {
        await using var processor = fixture.CreateFactory(new Dictionary<string, string?>
        {
            ["Appointment:PendingExpiry:Enabled"] = "true",
            ["Appointment:PendingExpiry:PollSeconds"] = "1",
            ["Appointment:PendingExpiry:Hours"] = "48",
        });
        _ = processor.Server; // sunucuyu (ve arka plan hizmetini) başlat

        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        await BackdateAsync(id, Overdue);

        var deadline = DateTime.UtcNow.AddSeconds(20);
        string status;
        do
        {
            await Task.Delay(300);
            status = (await RawStateAsync(id)).Status;
        }
        while (status != "REJECTED" && DateTime.UtcNow < deadline);

        status.Should().Be("REJECTED");
    }
}
