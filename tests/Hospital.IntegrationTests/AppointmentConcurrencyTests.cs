using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>Eşzamanlılık: gerçek HTTP istekleri + gerçek PostgreSQL. Çakışma kodda ön kontrolle, yarışta DB kısıtıyla engellenir.</summary>
public class AppointmentConcurrencyTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<int> RowCountAsync(Guid doctorId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Appointments.AsNoTracking().CountAsync(a => a.DoctorId == doctorId);
    }

    [Fact]
    public async Task Yirmi_hasta_ayni_slotu_ayni_anda_ister_tam_bir_basarili_digerleri_409()
    {
        const int parallelism = 20;
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patients = new List<ApiClient>();
        for (var i = 0; i < parallelism; i++)
        {
            patients.Add(await AppointmentKit.NewPatientAsync(fixture));
        }

        var gate = new TaskCompletionSource();
        var tasks = patients.Select(patient => Task.Run(async () =>
        {
            await gate.Task; // hepsi hazır olunca aynı anda gönder
            return await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(10, 0));
        })).ToList();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(parallelism - 1);

        // Kaybedenler anlaşılır bir kodla döner (ön kontrol: slot_unavailable, yarış: slot_taken).
        var codes = new List<string>();
        foreach (var loser in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            codes.Add(await AppointmentKit.CodeOfAsync(loser));
        }

        codes.Should().OnlyContain(code => code == "appointment.slot_unavailable" || code == "appointment.slot_taken");
        (await RowCountAsync(setup.DoctorId)).Should().Be(1);
    }

    [Fact]
    public async Task Ayni_hasta_ayni_anda_iki_doktordan_cakisan_saat_ister_yalniz_biri_olusur()
    {
        var first = await AppointmentKit.NewSetupAsync(fixture);
        var second = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var gate = new TaskCompletionSource();
        var tasks = new[] { (first, 0), (second, 15) }.Select(pair => Task.Run(async () =>
        {
            await gate.Task;
            return await AppointmentKit.BookAsync(patient, pair.Item1, pair.Item1.LocalToUtc(10, pair.Item2));
        })).ToList();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        (await RowCountAsync(first.DoctorId) + await RowCountAsync(second.DoctorId)).Should().Be(1);
    }

    [Fact]
    public async Task Ayni_randevuyu_ayni_anda_onaylayan_ve_reddeden_doktorda_yalniz_biri_basarili()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var gate = new TaskCompletionSource();
        var approve = Task.Run(async () =>
        {
            await gate.Task;
            return await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");
        });
        var reject = Task.Run(async () =>
        {
            await gate.Task;
            return await setup.Doctor.PostAsync($"/api/appointments/{id}/reject", new { Reason = "Müsait değilim" });
        });

        gate.SetResult();
        var responses = await Task.WhenAll(approve, reject);

        // Biri 204; diğeri ya durum kuralı (invalid_transition) ya da xmin eşzamanlılık belirteci ile 409 alır.
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var status = await AppointmentKit.StatusOfAsync(patient, id);
        status.Should().BeOneOf("APPROVED", "REJECTED");
    }

    [Fact]
    public async Task Ayni_randevuyu_ayni_anda_iki_kez_iptal_eden_hastada_tek_basari()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Vazgeçtim" });
        })).ToList();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("CANCELLED");
    }
}
