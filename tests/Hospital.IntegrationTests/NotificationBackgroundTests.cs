using System.Net;
using AwesomeAssertions;
using Hospital.Domain.Entities.Notifications;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// Arka plan işleyici (BackgroundService) uçtan uca: randevu işlemi outbox'a yazar, işleyici kendi başına gönderir.
/// SMTP yerine test göndericisi kullanılır; gerçek SMTP için <see cref="MailpitDeliveryTests"/>.
/// </summary>
public class NotificationBackgroundTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static Dictionary<string, string?> FastProcessor() => new()
    {
        ["Email:Outbox:Enabled"] = "true",
        ["Email:Outbox:PollSeconds"] = "1",
        ["Email:Outbox:BaseDelaySeconds"] = "1",
        ["Email:Outbox:MaxDelaySeconds"] = "1",
        ["Email:Outbox:MaxAttempts"] = "100",
    };

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(250);
        }

        return await condition();
    }

    [Fact]
    public async Task Isleyici_bekleyen_e_postalari_kendiliginden_gonderir()
    {
        await using var processor = fixture.CreateFactory(FastProcessor());
        _ = processor.Server; // sunucuyu (ve arka plan hizmetini) başlat
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var patientEmail = await OutboxKit.EmailOfAsync(patient);

        await AppointmentKit.BookOkAsync(patient, setup);

        var delivered = await WaitUntilAsync(() => Task.FromResult(
            processor.EmailSender.Sent.Count(m => m.ToAddress == patientEmail || m.ToAddress == setup.DoctorEmail) == 2));
        delivered.Should().BeTrue("işleyici hasta ve doktor e-postasını kısa sürede göndermeli");

        processor.EmailSender.Sent.Single(m => m.ToAddress == patientEmail).Subject.Should().Be("Randevu talebiniz alındı");
        processor.EmailSender.Sent.Single(m => m.ToAddress == setup.DoctorEmail).Subject.Should().Be("Yeni randevu talebi onayınızı bekliyor");

        var allSent = await WaitUntilAsync(async () =>
            (await OutboxKit.ToAsync(processor, patientEmail)).All(m => m.Status == EmailStatus.Sent));
        allSent.Should().BeTrue();
    }

    [Fact]
    public async Task Smtp_kapaliyken_randevu_akisi_surer_isleyici_yeniden_dener_smtp_gelince_mesaj_gider()
    {
        await using var processor = fixture.CreateFactory(FastProcessor());
        processor.EmailSender.FailWith = new InvalidOperationException("SMTP kapalı (test)");
        _ = processor.Server;
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var patientEmail = await OutboxKit.EmailOfAsync(patient);

        // Ana akış SMTP'den bağımsız: talep ve onay sorunsuz tamamlanır.
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        (await setup.Doctor.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("APPROVED");

        // İşleyici dener, başarısız olur ve mesajı kaybetmeden bekletir (birden çok deneme).
        var retried = await WaitUntilAsync(async () =>
        {
            var rows = await OutboxKit.ToAsync(processor, patientEmail);
            return rows.Count == 2 && rows.All(m => m.AttemptCount >= 2 && m.Status == EmailStatus.Pending && m.LastError is not null);
        });
        retried.Should().BeTrue("başarısız denemeler sayılmalı ve mesaj bekleyen kalmalı");
        processor.EmailSender.Sent.Should().BeEmpty();

        // SMTP düzelir → bekleyen e-postalar gider.
        processor.EmailSender.FailWith = null;
        var delivered = await WaitUntilAsync(() => Task.FromResult(processor.EmailSender.Sent.Count(m => m.ToAddress == patientEmail) == 2));

        delivered.Should().BeTrue();
        processor.EmailSender.Sent.Where(m => m.ToAddress == patientEmail).Select(m => m.Subject)
            .Should().BeEquivalentTo(["Randevu talebiniz alındı", "Randevunuz onaylandı"]);
        (await OutboxKit.ToAsync(processor, patientEmail)).Should().OnlyContain(m => m.Status == EmailStatus.Sent);
    }
}
