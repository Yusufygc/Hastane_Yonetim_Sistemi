using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>Randevu yaşam döngüsü: talep → onay/red → iptal → tamamlama, sahiplik ve rol denetimleri.</summary>
public class AppointmentFlowTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    // ---------- Oluşturma ----------

    [Fact]
    public async Task Hasta_randevu_talep_eder_bekleyen_olusur_snapshot_alanlari_dolu()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture, serviceDurationMinutes: 30);
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var response = await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(9, 30), note: "Baş ağrısı");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();

        var appointment = await ApiClient.ReadJsonAsync(await patient.GetAsync($"/api/appointments/{id}"));
        appointment.GetProperty("status").GetString().Should().Be("PENDING");
        appointment.GetProperty("startAt").GetDateTimeOffset().Should().Be(setup.LocalToUtc(9, 30));
        appointment.GetProperty("endAt").GetDateTimeOffset().Should().Be(setup.LocalToUtc(10, 0));
        appointment.GetProperty("doctorId").GetGuid().Should().Be(setup.DoctorId);
        appointment.GetProperty("hospitalId").GetGuid().Should().Be(setup.HospitalId);
        appointment.GetProperty("durationMinutes").GetInt32().Should().Be(30);
        appointment.GetProperty("price").GetDecimal().Should().Be(300m);
        appointment.GetProperty("currency").GetString().Should().Be("TRY");
        appointment.GetProperty("patientNote").GetString().Should().Be("Baş ağrısı");
        appointment.GetProperty("doctorName").GetString().Should().Be("Mehmet Demir");
        appointment.GetProperty("patientName").GetString().Should().Be("Ayşe Yılmaz");
    }

    [Fact]
    public async Task Slot_olmayan_saat_409_slot_unavailable()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var offGrid = await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(9, 7));
        var outsideWindow = await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(13, 0));
        var wrongDay = await AppointmentKit.BookAsync(patient, setup, setup.LocalToUtc(9, 0).AddDays(1));
        var past = await AppointmentKit.BookAsync(patient, setup, DateTimeOffset.UtcNow.AddDays(-1));

        foreach (var response in new[] { offGrid, outsideWindow, wrongDay, past })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await AppointmentKit.CodeOfAsync(response)).Should().Be("appointment.slot_unavailable");
        }
    }

    [Fact]
    public async Task Ayni_slotu_ikinci_hasta_alamaz_409()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (first, second) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        await AppointmentKit.BookOkAsync(first, setup, 10, 0);

        var sameSlot = await AppointmentKit.BookAsync(second, setup, setup.LocalToUtc(10, 0));
        var overlapping = await AppointmentKit.BookAsync(second, setup, setup.LocalToUtc(10, 15));
        var adjacent = await AppointmentKit.BookAsync(second, setup, setup.LocalToUtc(10, 30));

        sameSlot.StatusCode.Should().Be(HttpStatusCode.Conflict);
        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
        adjacent.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Hasta_ayni_saatte_iki_doktordan_randevu_alamaz_409_patient_overlap()
    {
        var first = await AppointmentKit.NewSetupAsync(fixture);
        var second = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        await AppointmentKit.BookOkAsync(patient, first, 10, 0);

        var response = await AppointmentKit.BookAsync(patient, second, second.LocalToUtc(10, 15));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(response)).Should().Be("appointment.patient_overlap");

        // Aynı hasta bitişik saatte başka doktordan alabilir.
        (await AppointmentKit.BookAsync(patient, second, second.LocalToUtc(10, 30))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Onaylanmamis_hizmet_icin_randevu_alinamaz_400()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var pendingService = await AdminSessions.CreateServiceAsync(setup.Admin, setup.HospitalId, setup.DepartmentId);
        await setup.Doctor.PostAsync("/api/doctors/me/services", new { ServiceId = pendingService });
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var response = await patient.PostAsync("/api/appointments", new
        {
            DoctorId = setup.DoctorId,
            ServiceId = pendingService,
            StartAt = setup.LocalToUtc(9, 0),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AppointmentKit.CodeOfAsync(response)).Should().Be("appointment.service_not_offered");
    }

    [Fact]
    public async Task Randevu_komutunda_dogrulama_hatalari_alan_bazli_400()
    {
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        var response = await patient.PostAsync("/api/appointments", new
        {
            DoctorId = Guid.Empty,
            ServiceId = Guid.Empty,
            StartAt = DateTimeOffset.MinValue,
            PatientNote = new string('x', 600),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ApiClient.ReadJsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("doctorId", out _).Should().BeTrue();
        errors.TryGetProperty("serviceId", out _).Should().BeTrue();
        errors.TryGetProperty("patientNote", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Yalniz_hasta_randevu_olusturur_anonim_401_doktor_yonetici_403()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);

        (await AppointmentKit.BookAsync(fixture.CreateClient(), setup, setup.LocalToUtc(9, 0))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AppointmentKit.BookAsync(setup.Doctor, setup, setup.LocalToUtc(9, 0))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AppointmentKit.BookAsync(setup.Admin, setup, setup.LocalToUtc(9, 0))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Onay / red ----------

    [Fact]
    public async Task Doktor_onaylar_durum_onayli_olur_ve_tekrar_onaylanamaz()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var approve = await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");
        var again = await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");

        approve.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("APPROVED");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(again)).Should().Be("appointment.invalid_transition");
    }

    [Fact]
    public async Task Baska_doktor_onaylayamaz_404_hasta_ve_yonetici_onaylayamaz_403()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var otherDoctor = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        (await otherDoctor.Doctor.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherDoctor.Doctor.PostAsync($"/api/appointments/{id}/reject", new { Reason = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherDoctor.Doctor.PostAsync($"/api/appointments/{id}/complete")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await patient.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await setup.Admin.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fixture.CreateClient().PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("PENDING");
    }

    [Fact]
    public async Task Red_neden_ister_reddedilen_randevu_slotu_birakir_ve_tekrar_islem_gormez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (first, second) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var id = await AppointmentKit.BookOkAsync(first, setup);

        var withoutReason = await setup.Doctor.PostAsync($"/api/appointments/{id}/reject", new { Reason = "" });
        withoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiClient.ReadJsonAsync(withoutReason)).GetProperty("errors").TryGetProperty("reason", out _).Should().BeTrue();

        (await setup.Doctor.PostAsync($"/api/appointments/{id}/reject", new { Reason = "O gün izinliyim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rejected = await ApiClient.ReadJsonAsync(await first.GetAsync($"/api/appointments/{id}"));
        rejected.GetProperty("status").GetString().Should().Be("REJECTED");
        rejected.GetProperty("decisionReason").GetString().Should().Be("O gün izinliyim");

        (await setup.Doctor.PostAsync($"/api/appointments/{id}/approve")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await setup.Doctor.PostAsync($"/api/appointments/{id}/reject", new { Reason = "tekrar" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await first.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Slot serbest: başka hasta aynı saati alabilir.
        await AppointmentKit.BookOkAsync(second, setup);
    }

    // ---------- İptal ----------

    [Fact]
    public async Task Hasta_bekleyen_randevusunu_iptal_eder_neden_zorunlu_tekrar_iptal_409()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var withoutReason = await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = " " });
        withoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Vazgeçtim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("CANCELLED");
        (await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Tekrar" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Baska_hasta_iptal_edemez_404_doktor_onayli_randevuyu_iptal_edebilir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (owner, stranger) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var id = await AppointmentKit.BookOkAsync(owner, setup);
        await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");

        (await stranger.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Sızma" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var byDoctor = await setup.Doctor.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Acil ameliyat" });

        byDoctor.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cancelled = await ApiClient.ReadJsonAsync(await owner.GetAsync($"/api/appointments/{id}"));
        cancelled.GetProperty("status").GetString().Should().Be("CANCELLED");
        cancelled.GetProperty("decisionReason").GetString().Should().Be("Acil ameliyat");
    }

    [Fact]
    public async Task Hasta_sure_siniri_icindeki_onayli_randevuyu_iptal_edemez_doktor_ve_yonetici_edebilir()
    {
        // Sınır 1000 saate çekilir: seed'deki yakın tarihli her onaylı randevu "sınır içinde" olur.
        var strict = fixture.CreateFactory(new Dictionary<string, string?> { ["Appointment:CancellationDeadlineHours"] = "1000" });
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture, strict);
        var pendingId = await AppointmentKit.BookOkAsync(patient, setup, 9, 0);
        var approvedId = await AppointmentKit.BookOkAsync(patient, setup, 10, 0);
        var approvedForDoctor = await AppointmentKit.BookOkAsync(await AppointmentKit.NewPatientAsync(fixture), setup, 11, 0);
        await setup.Doctor.PostAsync($"/api/appointments/{approvedId}/approve");
        await setup.Doctor.PostAsync($"/api/appointments/{approvedForDoctor}/approve");

        // Bekleyen randevu sürede olsa bile iptal edilir; onaylı randevu iptal edilemez.
        (await patient.PostAsync($"/api/appointments/{pendingId}/cancel", new { Reason = "Vazgeçtim" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var blocked = await patient.PostAsync($"/api/appointments/{approvedId}/cancel", new { Reason = "Gelemeyeceğim" });
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(blocked)).Should().Be("appointment.cancellation_deadline_passed");
        (await AppointmentKit.StatusOfAsync(patient, approvedId)).Should().Be("APPROVED");

        // Doktor ve yönetici bu sınıra tabi değil.
        var doctorStrict = new ApiClient(strict.CreateClient());
        await doctorStrict.LoginAsync(setup.DoctorEmail, TestDoctor.Password);
        (await doctorStrict.PostAsync($"/api/appointments/{approvedId}/cancel", new { Reason = "Doktor iptali" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await setup.Admin.PostAsync($"/api/appointments/{approvedForDoctor}/cancel", new { Reason = "Yönetici iptali" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Varsayilan_sinirda_uzak_tarihli_onayli_randevu_hasta_tarafindan_iptal_edilir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");

        var response = await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Plan değişti" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- Tamamlama ----------

    [Fact]
    public async Task Baslamamis_randevu_tamamlanamaz_baslamis_onayli_randevu_tamamlanir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var pending = await setup.Doctor.PostAsync($"/api/appointments/{id}/complete");
        pending.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(pending)).Should().Be("appointment.invalid_transition"); // bekleyen randevu tamamlanamaz

        await setup.Doctor.PostAsync($"/api/appointments/{id}/approve");
        var notStarted = await setup.Doctor.PostAsync($"/api/appointments/{id}/complete");
        notStarted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AppointmentKit.CodeOfAsync(notStarted)).Should().Be("appointment.not_started");

        await AppointmentKit.MoveToPastAsync(fixture, id);
        var completed = await setup.Doctor.PostAsync($"/api/appointments/{id}/complete");

        completed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("COMPLETED");
        (await setup.Doctor.PostAsync($"/api/appointments/{id}/complete")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await patient.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "geç" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------- Görüntüleme: sahiplik / kapsam (IDOR) ----------

    [Fact]
    public async Task Baskasinin_randevusu_404_olmayan_randevu_404_anonim_401()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var other = await AppointmentKit.NewSetupAsync(fixture);
        var (owner, stranger) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var id = await AppointmentKit.BookOkAsync(owner, setup);

        (await owner.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await setup.Doctor.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await stranger.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Doctor.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"/api/appointments/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await fixture.CreateClient().GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sube_yoneticisi_yalniz_kendi_subesinin_randevusunu_gorur_ve_iptal_eder()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var otherHospital = await AdminSessions.CreateHospitalAsync(setup.Admin);
        var ownManager = await AdminSessions.BranchManagerAsync(fixture, setup.HospitalId);
        var otherManager = await AdminSessions.BranchManagerAsync(fixture, otherHospital);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        (await otherManager.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherManager.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Sızma" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("PENDING");

        (await ownManager.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownManager.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "Şube kararı" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AppointmentKit.StatusOfAsync(patient, id)).Should().Be("CANCELLED");
    }

    [Fact]
    public async Task Personel_randevu_goremez_iptal_edemez_403()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var email = $"personel.{Guid.NewGuid():N}@test.local";
        (await setup.Admin.PostAsync("/api/staff", new
        {
            HospitalId = setup.HospitalId,
            Email = email,
            Password = TestAccount.ValidPassword,
            FirstName = "Mehmet",
            LastName = "Kaya",
            Title = "Hemşire",
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await AdminSessions.LoginAsync(fixture, email, TestAccount.ValidPassword);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        (await staff.GetAsync($"/api/appointments/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.PostAsync($"/api/appointments/{id}/cancel", new { Reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Listeler ----------

    [Fact]
    public async Task Hasta_kendi_randevularini_listeler_baskasininkini_gormez_durum_filtresi_calisir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var (patient, other) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var first = await AppointmentKit.BookOkAsync(patient, setup, 9, 0);
        var second = await AppointmentKit.BookOkAsync(patient, setup, 10, 0);
        await AppointmentKit.BookOkAsync(other, setup, 11, 0);
        await patient.PostAsync($"/api/appointments/{second}/cancel", new { Reason = "Vazgeçtim" });

        var all = await ApiClient.ReadJsonAsync(await patient.GetAsync("/api/patients/me/appointments"));
        var cancelled = await ApiClient.ReadJsonAsync(await patient.GetAsync("/api/patients/me/appointments?status=cancelled"));

        all.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().BeEquivalentTo([first, second]);
        // Yeniden eskiye sıralı: 10:00 randevusu (daha geç) önce gelir.
        all[0].GetProperty("id").GetGuid().Should().Be(second);
        cancelled.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(second);
        (await patient.GetAsync("/api/patients/me/appointments?status=bekliyor")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doktor_kendi_randevularini_ve_onayli_takvimini_gorur()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var other = await AppointmentKit.NewSetupAsync(fixture);
        var (p1, p2, p3) = (await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture), await AppointmentKit.NewPatientAsync(fixture));
        var approved = await AppointmentKit.BookOkAsync(p1, setup, 9, 0);
        var pending = await AppointmentKit.BookOkAsync(p2, setup, 10, 0);
        await AppointmentKit.BookOkAsync(p3, other, 9, 0);
        await setup.Doctor.PostAsync($"/api/appointments/{approved}/approve");

        var mine = await ApiClient.ReadJsonAsync(await setup.Doctor.GetAsync("/api/doctors/me/appointments"));
        var onlyPending = await ApiClient.ReadJsonAsync(await setup.Doctor.GetAsync("/api/doctors/me/appointments?status=PENDING"));
        var from = Uri.EscapeDataString(setup.LocalToUtc(0).ToString("o"));
        var to = Uri.EscapeDataString(setup.LocalToUtc(0).AddDays(1).ToString("o"));
        var schedule = await ApiClient.ReadJsonAsync(await setup.Doctor.GetAsync($"/api/doctors/me/schedule?from={from}&to={to}"));

        // Eskiden yeniye sıralı; yalnız kendi randevuları.
        mine.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Equal(approved, pending);
        onlyPending.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(pending);
        schedule.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(approved); // takvimde yalnız onaylı
    }

    [Fact]
    public async Task Yonetici_kapsamindaki_randevulari_listeler_baska_sube_404()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var otherHospital = await AdminSessions.CreateHospitalAsync(setup.Admin);
        var ownManager = await AdminSessions.BranchManagerAsync(fixture, setup.HospitalId);
        var otherManager = await AdminSessions.BranchManagerAsync(fixture, otherHospital);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);

        var own = await ApiClient.ReadJsonAsync(await ownManager.GetAsync("/api/appointments?status=pending"));
        var other = await ApiClient.ReadJsonAsync(await otherManager.GetAsync("/api/appointments"));
        var superAdmin = await ApiClient.ReadJsonAsync(await setup.Admin.GetAsync($"/api/appointments?hospitalId={setup.HospitalId}"));

        own.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Contain(id);
        other.EnumerateArray().Should().BeEmpty();
        superAdmin.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Contain(id);
        (await otherManager.GetAsync($"/api/appointments?hospitalId={setup.HospitalId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await patient.GetAsync("/api/appointments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await setup.Doctor.GetAsync("/api/appointments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Liste_uclari_rol_siniri_hasta_doktor_ucuna_doktor_hasta_ucuna_giremez()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture);
        var patient = await AppointmentKit.NewPatientAsync(fixture);

        (await patient.GetAsync("/api/doctors/me/appointments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await patient.GetAsync("/api/doctors/me/schedule")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await setup.Doctor.GetAsync("/api/patients/me/appointments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fixture.CreateClient().GetAsync("/api/patients/me/appointments")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- Snapshot ----------

    [Fact]
    public async Task Hizmet_sonradan_degisse_de_randevudaki_ad_sure_ucret_sabit_kalir()
    {
        var setup = await AppointmentKit.NewSetupAsync(fixture, serviceDurationMinutes: 30);
        var patient = await AppointmentKit.NewPatientAsync(fixture);
        var id = await AppointmentKit.BookOkAsync(patient, setup);
        var before = await ApiClient.ReadJsonAsync(await patient.GetAsync($"/api/appointments/{id}"));

        var update = await setup.Admin.PutAsync($"/api/services/{setup.ServiceId}", new
        {
            DepartmentId = setup.DepartmentId,
            Name = "Yeni Ad",
            DurationMinutes = 45,
            Price = 999m,
            Currency = "USD",
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await ApiClient.ReadJsonAsync(await patient.GetAsync($"/api/appointments/{id}"));
        after.GetProperty("serviceName").GetString().Should().Be(before.GetProperty("serviceName").GetString());
        after.GetProperty("durationMinutes").GetInt32().Should().Be(30);
        after.GetProperty("price").GetDecimal().Should().Be(300m);
        after.GetProperty("currency").GetString().Should().Be("TRY");
        after.GetProperty("endAt").GetDateTimeOffset().Should().Be(setup.LocalToUtc(9, 30));
    }
}
