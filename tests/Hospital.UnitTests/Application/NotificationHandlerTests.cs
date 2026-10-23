using AwesomeAssertions;
using Hospital.Application.Features.Notifications;
using Hospital.Application.Features.Notifications.EventHandlers;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Notifications;

namespace Hospital.UnitTests.Application;

public class NotificationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 6, 30, 0, TimeSpan.Zero); // yerel 09:30 (UTC+3)

    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid HospitalId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private static readonly AppointmentRecipients Recipients = new(
        "hasta@example.test", "Ayşe Yılmaz", "doktor@example.test", "Mehmet Demir", "Merkez Şube", "Europe/Istanbul");

    private sealed class FakeReader(AppointmentRecipients? recipients) : INotificationReader
    {
        public Task<AppointmentRecipients?> GetAppointmentRecipientsAsync(Guid patientId, Guid doctorId, Guid hospitalId, CancellationToken cancellationToken) =>
            Task.FromResult(recipients);
    }

    private sealed class FakeOutbox : IEmailOutbox
    {
        public List<EmailOutboxMessage> Messages { get; } = [];

        public Task<bool> EnqueueAsync(EmailOutboxMessage message, CancellationToken cancellationToken)
        {
            if (Messages.Any(m => m.DedupKey == message.DedupKey))
            {
                return Task.FromResult(false);
            }

            Messages.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (FakeOutbox Outbox, T Handler) Create<T>(Func<FakeReader, FakeOutbox, TimeProvider, T> factory, AppointmentRecipients? recipients = null)
    {
        var outbox = new FakeOutbox();
        return (outbox, factory(new FakeReader(recipients ?? Recipients), outbox, new FixedClock()));
    }

    // ---------- Talep ----------

    [Fact]
    public async Task Talep_hastaya_ve_doktora_birer_e_posta_ekler_dedup_anahtari_olay_ve_alici()
    {
        var (outbox, handler) = Create((r, o, c) => new AppointmentRequestedEmailHandler(r, o, c));
        var e = new AppointmentRequestedEvent(Guid.NewGuid(), PatientId, DoctorId, HospitalId, "Genel Muayene", Start, Start.AddMinutes(30));

        await handler.HandleAsync(e, CancellationToken.None);

        outbox.Messages.Should().HaveCount(2);
        var patient = outbox.Messages.Single(m => m.ToAddress == "hasta@example.test");
        var doctor = outbox.Messages.Single(m => m.ToAddress == "doktor@example.test");
        patient.TemplateKey.Should().Be("appointment.requested.patient");
        patient.DedupKey.Should().Be($"{e.EventId:N}:patient");
        patient.Body.Should().Contain("Genel Muayene").And.Contain("09:30").And.Contain("02.11.2026").And.Contain("Merkez Şube");
        doctor.TemplateKey.Should().Be("appointment.requested.doctor");
        doctor.DedupKey.Should().Be($"{e.EventId:N}:doctor");
        doctor.Body.Should().Contain("Ayşe Yılmaz");
        outbox.Messages.Should().OnlyContain(m => m.Status == EmailStatus.Pending && m.CreatedAt == Now);
    }

    [Fact]
    public async Task Ayni_olay_iki_kez_dagitilsa_bile_ikinci_e_posta_eklenmez()
    {
        var (outbox, handler) = Create((r, o, c) => new AppointmentRequestedEmailHandler(r, o, c));
        var e = new AppointmentRequestedEvent(Guid.NewGuid(), PatientId, DoctorId, HospitalId, "Genel Muayene", Start, Start.AddMinutes(30));

        await handler.HandleAsync(e, CancellationToken.None);
        await handler.HandleAsync(e, CancellationToken.None);

        outbox.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Alici_bulunamazsa_e_posta_eklenmez_hata_firlatmaz()
    {
        var outbox = new FakeOutbox();
        var handler = new AppointmentRequestedEmailHandler(new FakeReader(null), outbox, new FixedClock());

        await handler.HandleAsync(
            new AppointmentRequestedEvent(Guid.NewGuid(), PatientId, DoctorId, HospitalId, "x", Start, Start.AddMinutes(30)),
            CancellationToken.None);

        outbox.Messages.Should().BeEmpty();
    }

    // ---------- Onay / red ----------

    [Fact]
    public async Task Onay_yalniz_hastaya_gider()
    {
        var (outbox, handler) = Create((r, o, c) => new AppointmentApprovedEmailHandler(r, o, c));

        await handler.HandleAsync(new AppointmentApprovedEvent(Guid.NewGuid(), PatientId, DoctorId, HospitalId, "Genel Muayene", Start), CancellationToken.None);

        var message = outbox.Messages.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("hasta@example.test");
        message.TemplateKey.Should().Be("appointment.approved.patient");
        message.Subject.Should().Be("Randevunuz onaylandı");
    }

    [Fact]
    public async Task Red_yalniz_hastaya_gider_nedeni_icerir()
    {
        var (outbox, handler) = Create((r, o, c) => new AppointmentRejectedEmailHandler(r, o, c));

        await handler.HandleAsync(
            new AppointmentRejectedEvent(Guid.NewGuid(), PatientId, DoctorId, HospitalId, "Genel Muayene", Start, "O gün izinliyim"),
            CancellationToken.None);

        var message = outbox.Messages.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("hasta@example.test");
        message.Body.Should().Contain("O gün izinliyim");
    }

    // ---------- İptal: alıcı kimin iptal ettiğine göre ----------

    [Theory]
    [InlineData("patient", new[] { "doktor@example.test" }, "hasta")]
    [InlineData("doctor", new[] { "hasta@example.test" }, "doktor")]
    [InlineData("admin", new[] { "hasta@example.test", "doktor@example.test" }, "yönetici")]
    public async Task Iptal_karsi_tarafa_gider(string cancelledByKind, string[] expectedRecipients, string actorText)
    {
        var (outbox, handler) = Create((r, o, c) => new AppointmentCancelledEmailHandler(r, o, c));
        var cancelledBy = cancelledByKind switch { "patient" => PatientId, "doctor" => DoctorId, _ => AdminId };

        await handler.HandleAsync(
            new AppointmentCancelledEvent(Guid.NewGuid(), PatientId, DoctorId, HospitalId, "Genel Muayene", Start, cancelledBy, "Plan değişti"),
            CancellationToken.None);

        outbox.Messages.Select(m => m.ToAddress).Should().BeEquivalentTo(expectedRecipients);
        outbox.Messages.Should().OnlyContain(m => m.Body.Contains($"{actorText} tarafından") && m.Body.Contains("Plan değişti"));
    }

    // ---------- Şablon yardımcıları ----------

    [Fact]
    public void Tarih_saat_subenin_saat_diliminde_yazilir()
    {
        AppointmentEmails.FormatLocal(Start, "Europe/Istanbul").Should().Be("02.11.2026 09:30");
        AppointmentEmails.FormatLocal(Start, "UTC").Should().Be("02.11.2026 06:30");
    }

    [Fact]
    public void Serbest_metin_notu_sablonlara_girmez()
    {
        var all = new[]
        {
            AppointmentEmails.RequestedForPatient(Recipients, "Hizmet", Start),
            AppointmentEmails.RequestedForDoctor(Recipients, "Hizmet", Start),
            AppointmentEmails.Approved(Recipients, "Hizmet", Start),
        };

        // Şablon girdileri yalnız ad, hizmet, şube ve zamandır; kişisel/serbest alan sızmaz.
        all.Should().OnlyContain(c => !c.Body.Contains("@") && c.Body.Contains("Şube: Merkez Şube"));
    }
}
