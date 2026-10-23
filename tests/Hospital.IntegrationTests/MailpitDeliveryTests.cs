using System.Net;
using AwesomeAssertions;
using Hospital.Domain.Entities.Notifications;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// Gerçek SMTP (MailKit) → yerel Mailpit: randevu olayları sonucunda doğru e-postalar gerçekten teslim edilir.
/// Önkoşul: <c>docker compose up -d</c> (Mailpit SMTP 1025, API 8025).
/// </summary>
public class MailpitDeliveryTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly MailpitClient _mailpit = new();

    private HospitalApiFactory NewRealSmtpFactory() => fixture.CreateFactory(
        new Dictionary<string, string?>
        {
            ["Email:Outbox:Enabled"] = "true",
            ["Email:Outbox:PollSeconds"] = "1",
            ["Email:Smtp:Host"] = _mailpit.Host,
            ["Email:Smtp:Port"] = MailpitClient.SmtpPort.ToString(),
            ["Email:Smtp:Security"] = "None",
        },
        useFakeEmailSender: false);

    [Fact]
    public async Task Randevu_talebi_hasta_ve_doktora_Mailpit_te_dogru_e_posta_olarak_ulasir()
    {
        await _mailpit.EnsureAvailableAsync();
        await using var factory = NewRealSmtpFactory();
        _ = factory.Server;
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var patientEmail = await OutboxKit.EmailOfAsync(patient);

        await AppointmentKit.BookOkAsync(patient, setup, 9, 30);

        var toPatient = (await _mailpit.WaitForMessagesAsync(patientEmail)).Should().ContainSingle().Subject;
        var toDoctor = (await _mailpit.WaitForMessagesAsync(setup.DoctorEmail)).Should().ContainSingle().Subject;

        toPatient.Subject.Should().Be("Randevu talebiniz alındı"); // Türkçe karakterler bozulmadan
        toPatient.FromAddress.Should().Be("noreply@hastane.local");
        toPatient.To.Should().Equal(patientEmail);
        toPatient.Text.Should().Contain("Ayşe Yılmaz").And.Contain("Mehmet Demir").And.Contain("09:30").And.Contain("Europe/Istanbul");

        toDoctor.Subject.Should().Be("Yeni randevu talebi onayınızı bekliyor");
        toDoctor.Text.Should().Contain("Ayşe Yılmaz");

        // Gönderilenler outbox'ta SENT olarak işaretlenir.
        (await OutboxKit.ToAsync(factory, patientEmail)).Should().OnlyContain(m => m.Status == EmailStatus.Sent && m.SentAt != null);
    }

    [Fact]
    public async Task Onay_red_ve_iptal_e_postalari_ilgili_kisiye_ulasir()
    {
        await _mailpit.EnsureAvailableAsync();
        await using var factory = NewRealSmtpFactory();
        _ = factory.Server;
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (approved, rejected, cancelled) = (
            await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var (approvedEmail, rejectedEmail) = (await OutboxKit.EmailOfAsync(approved), await OutboxKit.EmailOfAsync(rejected));

        var approvedId = await AppointmentKit.BookOkAsync(approved, setup, 9, 0);
        var rejectedId = await AppointmentKit.BookOkAsync(rejected, setup, 10, 0);
        var cancelledId = await AppointmentKit.BookOkAsync(cancelled, setup, 11, 0);

        (await setup.Doctor.PostAsync($"/api/appointments/{approvedId}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await setup.Doctor.PostAsync($"/api/appointments/{rejectedId}/reject", new { Reason = "Bu hizmeti o gün vermiyorum" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cancelled.PostAsync($"/api/appointments/{cancelledId}/cancel", new { Reason = "Vazgeçtim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approvedMessages = await _mailpit.WaitForMessagesAsync(approvedEmail, expected: 2);
        approvedMessages.Select(m => m.Subject).Should().BeEquivalentTo(["Randevu talebiniz alındı", "Randevunuz onaylandı"]);

        var rejectedMessages = await _mailpit.WaitForMessagesAsync(rejectedEmail, expected: 2);
        rejectedMessages.Single(m => m.Subject == "Randevu talebiniz onaylanmadı").Text.Should().Contain("Bu hizmeti o gün vermiyorum");

        // Doktor: 3 talep + hasta iptali = 4 e-posta.
        var doctorMessages = await _mailpit.WaitForMessagesAsync(setup.DoctorEmail, expected: 4);
        doctorMessages.Select(m => m.Subject).Should().Contain("Bir randevu iptal edildi");
        doctorMessages.Single(m => m.Subject == "Bir randevu iptal edildi").Text.Should().Contain("hasta tarafından").And.Contain("Vazgeçtim");
    }
}
