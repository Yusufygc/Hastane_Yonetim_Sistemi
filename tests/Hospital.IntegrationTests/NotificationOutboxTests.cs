using System.Net;
using AwesomeAssertions;
using Hospital.Application.Exceptions;
using Hospital.Application.Features.Notifications;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Notifications;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests;

/// <summary>
/// Transactional Outbox: olaylar aynı transaction'da e-posta satırına çevrilir; <see cref="IEmailOutboxStore"/> güvenli
/// gönderir (yeniden deneme, geri çekilme, vazgeçme, çift gönderim yok). Arka plan işleyici bu sınıfta kapalıdır;
/// işleme deterministik olarak mağaza üzerinden yürütülür.
/// </summary>
public class NotificationOutboxTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HospitalApiFactory Factory => fixture.Factory;

    private async Task<(BookingSetup Setup, ApiClient Patient, string PatientEmail)> NewBookingAsync()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        return (setup, patient, await OutboxKit.EmailOfAsync(patient));
    }

    // ---------- Olay → outbox satırı (aynı transaction) ----------

    [Fact]
    public async Task Randevu_talebi_hasta_ve_doktor_icin_bekleyen_outbox_satiri_uretir()
    {
        var (setup, patient, patientEmail) = await NewBookingAsync();
        var response = await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(9, 30), note: "Çok gizli kişisel not");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();
        var serviceName = (await ApiClient.ReadJsonAsync(await patient.GetAsync($"/api/appointments/{id}"))).GetProperty("serviceName").GetString()!;

        var patientMail = (await OutboxKit.ToAsync(Factory, patientEmail)).Should().ContainSingle().Subject;
        var doctorMail = (await OutboxKit.ToAsync(Factory, setup.DoctorEmail)).Should().ContainSingle().Subject;

        patientMail.Status.Should().Be(EmailStatus.Pending);
        patientMail.AttemptCount.Should().Be(0);
        patientMail.SentAt.Should().BeNull();
        patientMail.TemplateKey.Should().Be("appointment.requested.patient");
        patientMail.Subject.Should().Be("Randevu talebiniz alındı");
        patientMail.ToName.Should().Be("Ayşe Yılmaz");
        patientMail.Body.Should().Contain(serviceName).And.Contain("Mehmet Demir").And.Contain("09:30")
            .And.Contain("Europe/Istanbul").And.Contain($"{setup.Date:dd.MM.yyyy}");

        doctorMail.TemplateKey.Should().Be("appointment.requested.doctor");
        doctorMail.Subject.Should().Be("Yeni randevu talebi onayınızı bekliyor");
        doctorMail.Body.Should().Contain("Ayşe Yılmaz").And.Contain(serviceName);

        // Serbest metin notu e-postaya konmaz; tekrar önleme anahtarları olay kimliği + alıcıdır.
        patientMail.Body.Should().NotContain("gizli");
        doctorMail.Body.Should().NotContain("gizli");
        patientMail.DedupKey.Should().EndWith(":patient");
        doctorMail.DedupKey.Should().EndWith(":doctor");
        patientMail.DedupKey.Split(':')[0].Should().Be(doctorMail.DedupKey.Split(':')[0]); // aynı olay
    }

    [Fact]
    public async Task Onay_hastaya_red_neden_ile_hastaya_e_posta_uretir()
    {
        var (setup, patient, patientEmail) = await NewBookingAsync();
        var other = await AppointmentKit.NewPatientAsync(fixture);
        var otherEmail = await OutboxKit.EmailOfAsync(other);
        var approvedId = await AppointmentKit.BookOkAsync(patient, setup, 9, 0);
        var rejectedId = await AppointmentKit.BookOkAsync(other, setup, 10, 0);

        (await setup.Doctor.PostAsync($"/api/appointments/{approvedId}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await setup.Doctor.PostAsync($"/api/appointments/{rejectedId}/reject", new { Reason = "O gün izinliyim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approved = (await OutboxKit.ToAsync(Factory, patientEmail)).Single(m => m.TemplateKey == "appointment.approved.patient");
        approved.Subject.Should().Be("Randevunuz onaylandı");
        approved.Body.Should().Contain("onaylandı").And.Contain("09:00");

        var rejected = (await OutboxKit.ToAsync(Factory, otherEmail)).Single(m => m.TemplateKey == "appointment.rejected.patient");
        rejected.Subject.Should().Be("Randevu talebiniz onaylanmadı");
        rejected.Body.Should().Contain("O gün izinliyim").And.Contain("10:00");

        // Doktora onay/red e-postası gitmez.
        (await OutboxKit.ToAsync(Factory, setup.DoctorEmail)).Select(m => m.TemplateKey).Should().OnlyContain(k => k == "appointment.requested.doctor");
    }

    [Fact]
    public async Task Iptal_karsi_tarafa_gider_hasta_iptali_doktora_doktor_iptali_hastaya_yonetici_iptali_ikisine()
    {
        var (setup, patient, patientEmail) = await NewBookingAsync();
        var second = await AppointmentKit.NewPatientAsync(fixture);
        var third = await AppointmentKit.NewPatientAsync(fixture);
        var (secondEmail, thirdEmail) = (await OutboxKit.EmailOfAsync(second), await OutboxKit.EmailOfAsync(third));

        var byPatient = await AppointmentKit.BookOkAsync(patient, setup, 9, 0);
        var byDoctor = await AppointmentKit.BookOkAsync(second, setup, 10, 0);
        var byAdmin = await AppointmentKit.BookOkAsync(third, setup, 11, 0);

        (await patient.PostAsync($"/api/appointments/{byPatient}/cancel", new { Reason = "Vazgeçtim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await setup.Doctor.PostAsync($"/api/appointments/{byDoctor}/cancel", new { Reason = "Acil ameliyat" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await setup.Admin.PostAsync($"/api/appointments/{byAdmin}/cancel", new { Reason = "Şube kararı" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var doctorRows = await OutboxKit.ToAsync(Factory, setup.DoctorEmail);
        var patientRows = await OutboxKit.ToAsync(Factory, patientEmail);
        var secondRows = await OutboxKit.ToAsync(Factory, secondEmail);
        var thirdRows = await OutboxKit.ToAsync(Factory, thirdEmail);

        // Hasta iptal etti → doktora; hastaya "iptal" e-postası yok (yalnız kendi talep bildirimi).
        patientRows.Select(m => m.TemplateKey).Should().NotContain("appointment.cancelled.patient");
        doctorRows.Should().Contain(m => m.TemplateKey == "appointment.cancelled.doctor" && m.Body.Contains("hasta tarafından") && m.Body.Contains("Vazgeçtim"));

        // Doktor iptal etti → hastaya; doktora o iptal için e-posta yok.
        var toSecond = secondRows.Single(m => m.TemplateKey == "appointment.cancelled.patient");
        toSecond.Subject.Should().Be("Randevunuz iptal edildi");
        toSecond.Body.Should().Contain("doktor tarafından").And.Contain("Acil ameliyat");
        doctorRows.Should().NotContain(m => m.TemplateKey == "appointment.cancelled.doctor" && m.Body.Contains("Acil ameliyat"));

        // Yönetici iptal etti → ikisine.
        thirdRows.Should().Contain(m => m.TemplateKey == "appointment.cancelled.patient" && m.Body.Contains("yönetici tarafından") && m.Body.Contains("Şube kararı"));
        doctorRows.Should().Contain(m => m.TemplateKey == "appointment.cancelled.doctor" && m.Body.Contains("yönetici tarafından"));
    }

    [Fact]
    public async Task Basarisiz_istekler_outbox_satiri_uretmez()
    {
        var (setup, patient, _) = await NewBookingAsync();
        var id = await AppointmentKit.BookOkAsync(patient, setup, 9, 0);
        await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");
        var before = (await OutboxKit.RowsAsync(Factory)).Count;

        var notASlot = await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(9, 7));                    // 409 slot yok
        var again = await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");                              // 409 geçersiz geçiş
        var forbidden = await patient.PostAsync($"/api/appointments/{id}/approve");                              // 403
        var noReason = await setup.Doctor.PostAsync($"/api/appointments/{id}/reject", new { Reason = "" });      // 400
        var otherDoctor = await (await AppointmentKit.NewSetupAsync(fixture)).Doctor.PostAsync($"/api/appointments/{id}/approve"); // 404

        notASlot.StatusCode.Should().Be(HttpStatusCode.Conflict);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        otherDoctor.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await OutboxKit.RowsAsync(Factory)).Count.Should().Be(before);
    }

    [Fact]
    public async Task Veritabani_cakismasiyla_geri_alinan_kayit_outbox_satirini_da_geri_alir_atomiklik()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (first, second) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var (firstId, secondId) = (await OutboxKit.IdOfAsync(first), await OutboxKit.IdOfAsync(second));
        var (firstEmail, secondEmail) = (await OutboxKit.EmailOfAsync(first), await OutboxKit.EmailOfAsync(second));
        var start = setup.LocalToUtc(10, 0);

        async Task BookDirectAsync(Guid patientId)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = await db.Services.AsNoTracking().SingleAsync(s => s.Id == setup.ServiceId);

            // Uygulama ön kontrollerini atlar: yalnızca DB exclusion kısıtı korur (eşzamanlı yarışın kaybedeni).
            db.Appointments.Add(Appointment.Request(
                patientId, setup.DoctorId, setup.HospitalId, ServiceSnapshot.From(service), start, start.AddMinutes(30), null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        await BookDirectAsync(firstId);
        var act = () => BookDirectAsync(secondId);

        await act.Should().ThrowAsync<DataConflictException>();

        // Kazanan: hasta + doktor satırı; kaybeden: hiç satır yok (aynı transaction geri alındı).
        (await OutboxKit.ToAsync(Factory, firstEmail)).Should().ContainSingle();
        (await OutboxKit.ToAsync(Factory, setup.DoctorEmail)).Should().ContainSingle();
        (await OutboxKit.ToAsync(Factory, secondEmail)).Should().BeEmpty();
    }

    [Fact]
    public async Task Yirmi_paralel_talepte_doktora_tam_bir_e_posta_satiri_olusur()
    {
        const int parallelism = 20;
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patients = new List<ApiClient>();
        for (var i = 0; i < parallelism; i++)
        {
            patients.Add(await AppointmentKit.NewPatientAsync(fixture));
        }

        var gate = new TaskCompletionSource();
        var tasks = patients.Select(p => Task.Run(async () =>
        {
            await gate.Task;
            return await AppointmentKit.BookAsync(p, setup, setup.LocalToUtc(10, 0));
        })).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        // Yalnız kazanan için "doktora yeni talep" e-postası; 19 kaybeden için hiçbir şey kuyruğa girmedi.
        (await OutboxKit.ToAsync(Factory, setup.DoctorEmail)).Should().ContainSingle();
    }

    // ---------- Tekrar önleme (dedup_key) ----------

    [Fact]
    public async Task Ayni_olay_ve_alici_icin_ikinci_e_posta_eklenmez()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = $"{Guid.NewGuid():N}:patient";

        EmailOutboxMessage New() =>
            EmailOutboxMessage.Create(key, "a@test.local", "A", "Konu", "İçerik", "test.template", DateTimeOffset.UtcNow);

        var first = await outbox.EnqueueAsync(New(), CancellationToken.None);
        var sameBatch = await outbox.EnqueueAsync(New(), CancellationToken.None);
        await db.SaveChangesAsync();
        var afterSave = await outbox.EnqueueAsync(New(), CancellationToken.None);

        first.Should().BeTrue();
        sameBatch.Should().BeFalse();
        afterSave.Should().BeFalse();
        (await OutboxKit.RowsAsync(Factory)).Count(m => m.DedupKey == key).Should().Be(1);
    }

    [Fact]
    public async Task Veritabani_ayni_dedup_anahtarini_benzersiz_indeksle_reddeder()
    {
        var key = $"{Guid.NewGuid():N}:doctor";
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EmailOutbox.Add(EmailOutboxMessage.Create(key, "a@test.local", null, "Konu", "İçerik", "t", DateTimeOffset.UtcNow));
        db.EmailOutbox.Add(EmailOutboxMessage.Create(key, "b@test.local", null, "Konu", "İçerik", "t", DateTimeOffset.UtcNow));

        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DataConflictException>()).Which.ConstraintName.Should().Be("ix_email_outbox_dedup_key");
    }

    // ---------- İşleme: gönderim, yeniden deneme, geri çekilme, vazgeçme ----------

    [Fact]
    public async Task Bekleyen_mesaj_gonderilir_gonderildi_olarak_islenir_ve_bir_daha_gonderilmez()
    {
        await OutboxKit.DrainAsync(Factory);
        var (setup, patient, patientEmail) = await NewBookingAsync();
        await AppointmentKit.BookOkAsync(patient, setup);
        var sender = new FakeEmailSender();

        var first = await OutboxKit.ProcessAllAsync(Factory, sender);
        var second = await OutboxKit.ProcessAllAsync(Factory, sender);

        first.Should().HaveCount(2).And.OnlyContain(r => r.Outcome == OutboxOutcome.Sent);
        second.Should().BeEmpty(); // gönderilenler tekrar işlenmez
        sender.Sent.Should().HaveCount(2);
        sender.Sent.Select(m => m.ToAddress).Should().BeEquivalentTo([patientEmail, setup.DoctorEmail]);
        sender.Sent.Single(m => m.ToAddress == patientEmail).Subject.Should().Be("Randevu talebiniz alındı");

        var rows = (await OutboxKit.ToAsync(Factory, patientEmail)).Single();
        rows.Status.Should().Be(EmailStatus.Sent);
        rows.AttemptCount.Should().Be(1);
        rows.SentAt.Should().NotBeNull();
        rows.LastError.Should().BeNull();
    }

    [Fact]
    public async Task Smtp_hatasinda_mesaj_bekler_ussel_geri_cekilmeyle_yeniden_denenir_sonra_gider()
    {
        await OutboxKit.DrainAsync(Factory);
        var (setup, patient, patientEmail) = await NewBookingAsync();
        await AppointmentKit.BookOkAsync(patient, setup);
        var sender = new FakeEmailSender { FailWith = new InvalidOperationException("SMTP sunucusuna bağlanılamadı") };
        var now = DateTimeOffset.UtcNow;

        var failed = await OutboxKit.ProcessAllAsync(Factory, sender, now);

        failed.Should().HaveCount(2).And.OnlyContain(r => r.Outcome == OutboxOutcome.RetryScheduled);
        var row = (await OutboxKit.ToAsync(Factory, patientEmail)).Single();
        row.Status.Should().Be(EmailStatus.Pending); // kaybolmadı
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().Contain("SMTP sunucusuna bağlanılamadı");
        row.NextAttemptAt.Should().BeCloseTo(now.AddSeconds(30), TimeSpan.FromSeconds(2)); // 1. deneme sonrası 30 sn

        // Geri çekilme süresi dolmadan tekrar denenmez.
        (await OutboxKit.ProcessAllAsync(Factory, sender, now.AddSeconds(10))).Should().BeEmpty();

        // SMTP düzelince süre dolunca gider.
        sender.FailWith = null;
        var recovered = await OutboxKit.ProcessAllAsync(Factory, sender, now.AddSeconds(31));

        recovered.Should().HaveCount(2).And.OnlyContain(r => r.Outcome == OutboxOutcome.Sent);
        var sent = (await OutboxKit.ToAsync(Factory, patientEmail)).Single();
        sent.Status.Should().Be(EmailStatus.Sent);
        sent.AttemptCount.Should().Be(2);
        sent.LastError.Should().BeNull();
        sender.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deneme_siniri_asilinca_mesaj_FAILED_olur_ve_bir_daha_denenmez()
    {
        await OutboxKit.DrainAsync(Factory);
        var (setup, patient, patientEmail) = await NewBookingAsync();
        await AppointmentKit.BookOkAsync(patient, setup);
        var sender = new FakeEmailSender { FailWith = new InvalidOperationException("kalıcı hata") };
        var policy = new RetryPolicy(MaxAttempts: 3, BaseDelay: TimeSpan.FromSeconds(30), MaxDelay: TimeSpan.FromHours(1));
        var now = DateTimeOffset.UtcNow;

        var attempt1 = await OutboxKit.ProcessAllAsync(Factory, sender, now, policy);
        var attempt2 = await OutboxKit.ProcessAllAsync(Factory, sender, now.AddMinutes(2), policy);
        var attempt3 = await OutboxKit.ProcessAllAsync(Factory, sender, now.AddMinutes(10), policy);

        attempt1.Should().OnlyContain(r => r.Outcome == OutboxOutcome.RetryScheduled);
        attempt2.Should().OnlyContain(r => r.Outcome == OutboxOutcome.RetryScheduled);
        attempt3.Should().HaveCount(2).And.OnlyContain(r => r.Outcome == OutboxOutcome.GaveUp);

        var row = (await OutboxKit.ToAsync(Factory, patientEmail)).Single();
        row.Status.Should().Be(EmailStatus.Failed);
        row.AttemptCount.Should().Be(3);
        row.LastError.Should().Contain("kalıcı hata");

        sender.FailWith = null;
        (await OutboxKit.ProcessAllAsync(Factory, sender, now.AddDays(30), policy)).Should().BeEmpty(); // FAILED yeniden alınmaz
        sender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Kapali_smtp_randevu_islemlerini_bozmaz_mesaj_sonra_gider()
    {
        await OutboxKit.DrainAsync(Factory);
        var (setup, patient, patientEmail) = await NewBookingAsync();
        Factory.EmailSender.FailWith = new InvalidOperationException("SMTP kapalı");
        try
        {
            // SMTP kapalıyken tüm randevu akışı çalışır: gönderim istek yolunda değildir.
            var id = await AppointmentKit.BookOkAsync(patient, setup);
            (await setup.Doctor.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("APPROVED");

            var pending = (await OutboxKit.ToAsync(Factory, patientEmail)).Where(m => m.Status == EmailStatus.Pending).ToList();
            pending.Should().HaveCount(2); // talep + onay bekliyor
        }
        finally
        {
            Factory.EmailSender.Reset();
        }

        // SMTP gelince bekleyen tüm e-postalar gider.
        var sender = new FakeEmailSender();
        await OutboxKit.ProcessAllAsync(Factory, sender, DateTimeOffset.UtcNow.AddMinutes(1));

        sender.Sent.Where(m => m.ToAddress == patientEmail).Select(m => m.Subject)
            .Should().BeEquivalentTo(["Randevu talebiniz alındı", "Randevunuz onaylandı"]);
    }

    [Fact]
    public async Task Birden_cok_isleyici_ayni_mesaji_bir_kereden_fazla_gondermez_SKIP_LOCKED()
    {
        await OutboxKit.DrainAsync(Factory);
        var previousIds = (await OutboxKit.RowsAsync(Factory)).Select(m => m.Id).ToHashSet(); // önceki testlerin (ör. FAILED) satırları
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        const int appointmentCount = 5;

        for (var i = 0; i < appointmentCount; i++)
        {
            var patient = await AppointmentKit.NewPatientAsync(fixture);
            await AppointmentKit.BookOkAsync(patient, setup, 9 + i / 2, i % 2 * 30);
        }

        var pendingBefore = (await OutboxKit.RowsAsync(Factory)).Count(m => m.Status == EmailStatus.Pending && !previousIds.Contains(m.Id));
        var sender = new FakeEmailSender { Delay = TimeSpan.FromMilliseconds(40) }; // kilitler üst üste binsin

        // 4 işleyici aynı anda boşalana kadar çalışır.
        var workers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => OutboxKit.ProcessAllAsync(Factory, sender)))
            .ToList();
        var results = await Task.WhenAll(workers);

        pendingBefore.Should().Be(appointmentCount * 2);
        results.Sum(r => r.Count).Should().Be(pendingBefore);
        sender.Sent.Should().HaveCount(pendingBefore);
        sender.Sent.GroupBy(m => (m.ToAddress, m.Subject, m.Body)).Should().OnlyContain(g => g.Count() == 1);

        var rows = (await OutboxKit.RowsAsync(Factory)).Where(m => !previousIds.Contains(m.Id)).ToList();
        rows.Should().HaveCount(pendingBefore);
        rows.Where(m => m.Status != EmailStatus.Sent).Should().BeEmpty();
        rows.Should().OnlyContain(m => m.AttemptCount == 1);
    }

    [Fact]
    public async Task Gonderim_sirasi_olusturma_sirasidir_gelecek_tarihli_deneme_atlanir()
    {
        await OutboxKit.DrainAsync(Factory);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            var later = EmailOutboxMessage.Create($"{Guid.NewGuid():N}:a", "later@test.local", null, "İkinci", "x", "t", now.AddSeconds(2));
            var earlier = EmailOutboxMessage.Create($"{Guid.NewGuid():N}:b", "earlier@test.local", null, "Birinci", "x", "t", now.AddSeconds(1));
            db.EmailOutbox.AddRange(later, earlier);
            await db.SaveChangesAsync();
        }

        var sender = new FakeEmailSender();
        await OutboxKit.ProcessAllAsync(Factory, sender, DateTimeOffset.UtcNow.AddMinutes(1));

        sender.Sent.Select(m => m.Subject).Should().Equal("Birinci", "İkinci");
    }
}
