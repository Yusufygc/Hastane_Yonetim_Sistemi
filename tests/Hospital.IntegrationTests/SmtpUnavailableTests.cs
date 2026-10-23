using System.Net;
using AwesomeAssertions;
using Hospital.Domain.Entities.Notifications;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// Gerçek (kapalı) SMTP portu: bağlantı reddedilir. Randevu akışı bozulmaz; işleyici hatayı kaydeder, mesaj bekler ve
/// yeniden dener (çıkış kriteri: "SMTP kapalıyken randevu akışı bozulmaz, mail sonra gider").
/// </summary>
public class SmtpUnavailableTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Kapali_smtp_portunda_randevu_calisir_hata_kaydedilir_mesaj_bekler()
    {
        await using var factory = fixture.CreateFactory(
            new Dictionary<string, string?>
            {
                ["Email:Outbox:Enabled"] = "true",
                ["Email:Outbox:PollSeconds"] = "1",
                ["Email:Outbox:BaseDelaySeconds"] = "1",
                ["Email:Outbox:MaxDelaySeconds"] = "1",
                ["Email:Outbox:MaxAttempts"] = "100",
                ["Email:Smtp:Host"] = "127.0.0.1",
                ["Email:Smtp:Port"] = "1", // dinleyen yok
                ["Email:Smtp:Security"] = "None",
            },
            useFakeEmailSender: false);
        _ = factory.Server;

        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var patientEmail = await OutboxKit.EmailOfAsync(patient);

        var id = await AppointmentKit.BookOkAsync(patient, setup);
        (await setup.Doctor.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deadline = DateTime.UtcNow.AddSeconds(25);
        List<EmailOutboxMessage> rows;
        do
        {
            await Task.Delay(300);
            rows = await OutboxKit.ToAsync(factory, patientEmail);
        }
        while (DateTime.UtcNow < deadline && !(rows.Count == 2 && rows.All(m => m.AttemptCount >= 2)));

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(m => m.Status == EmailStatus.Pending && m.AttemptCount >= 2 && !string.IsNullOrEmpty(m.LastError));
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("APPROVED"); // randevu etkilenmedi
    }
}
