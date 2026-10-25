using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace Hospital.IntegrationTests;

/// <summary>
/// Performans bakışı: kritik sorguların uygun indeksi kullanabildiğini gerçek PostgreSQL planıyla doğrular. Tablolar küçük
/// olduğundan planlayıcı normalde ardışık tarama seçer; <c>enable_seqscan = off</c> ile ardışık tarama pahalı sayılır —
/// plan yine de "Seq Scan" içeriyorsa sorgunun kullanabileceği bir indeks yoktur (indeks eksikliği/regresyonu).
/// </summary>
public class QueryPlanTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<string> ExplainAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var set = new NpgsqlCommand("SET LOCAL enable_seqscan = off", connection, transaction))
        {
            await set.ExecuteNonQueryAsync();
        }

        await using var explain = new NpgsqlCommand($"EXPLAIN {sql}", connection, transaction);
        explain.Parameters.AddRange(parameters);

        var lines = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        return string.Join('\n', lines);
    }

    private static NpgsqlParameter Uuid(string name) => new(name, NpgsqlDbType.Uuid) { Value = Guid.NewGuid() };

    private static NpgsqlParameter Time(string name) => new(name, NpgsqlDbType.TimestampTz) { Value = DateTimeOffset.UtcNow };

    private static void AssertUsesIndex(string plan, string? expectedIndex = null)
    {
        plan.Should().NotContain("Seq Scan", "sorgu uygun bir indeksi kullanabilmeli\n" + plan);
        if (expectedIndex is not null)
        {
            plan.Should().Contain(expectedIndex, plan);
        }
    }

    [Fact]
    public async Task Doktorun_dolu_slotlari_indeks_veya_exclusion_indeksiyle_okunur()
    {
        var plan = await ExplainAsync(
            "SELECT start_at, end_at FROM appointment WHERE doctor_id = @doctor AND status IN ('PENDING', 'APPROVED') AND start_at < @end AND end_at > @start",
            Uuid("doctor"), Time("end"), Time("start"));

        AssertUsesIndex(plan);
    }

    [Fact]
    public async Task Hasta_gecmisi_indeksle_azalan_sirada_okunur()
    {
        var plan = await ExplainAsync(
            "SELECT id FROM appointment WHERE patient_id = @patient ORDER BY start_at DESC LIMIT 50", Uuid("patient"));

        AssertUsesIndex(plan, "ix_appointment_patient_id_start_at");
    }

    [Fact]
    public async Task Doktor_takvimi_doktor_ve_baslangic_indeksini_kullanir()
    {
        var plan = await ExplainAsync(
            "SELECT id FROM appointment WHERE doctor_id = @doctor AND start_at >= @from ORDER BY start_at LIMIT 50", Uuid("doctor"), Time("from"));

        AssertUsesIndex(plan, "ix_appointment_doctor_id_start_at");
    }

    [Fact]
    public async Task Yonetici_listesi_sube_durum_baslangic_indeksini_kullanir()
    {
        var plan = await ExplainAsync(
            "SELECT id FROM appointment WHERE hospital_id = @hospital AND status = 'APPROVED' ORDER BY start_at DESC LIMIT 50", Uuid("hospital"));

        AssertUsesIndex(plan, "ix_appointment_hospital_id_status_start_at");
    }

    [Fact]
    public async Task Bekleyen_talep_zaman_asimi_taramasi_kismi_indeksi_kullanir()
    {
        var plan = await ExplainAsync(
            "SELECT id FROM appointment WHERE status = 'PENDING' AND (created_at <= @createdBefore OR start_at <= @now) ORDER BY created_at, id LIMIT 50",
            Time("createdBefore"), Time("now"));

        AssertUsesIndex(plan, "ix_appointment_pending_created_at");
    }

    [Fact]
    public async Task Yonetici_onay_kuyrugu_kismi_indeksi_kullanir()
    {
        var plan = await ExplainAsync("SELECT doctor_id FROM doctor_service WHERE status = 'PENDING' ORDER BY created_at", []);

        AssertUsesIndex(plan, "ix_doctor_service_pending");
    }

    [Fact]
    public async Task E_posta_outbox_bekleyenleri_kismi_indeksle_bulunur()
    {
        var plan = await ExplainAsync(
            "SELECT id FROM email_outbox WHERE status = 'PENDING' AND next_attempt_at <= @now ORDER BY created_at LIMIT 1", Time("now"));

        AssertUsesIndex(plan, "ix_email_outbox_pending");
    }

    [Fact]
    public async Task Tekrar_onleme_ve_TC_kor_indeksi_aramalari_benzersiz_indeksle_yapilir()
    {
        var dedup = await ExplainAsync("SELECT id FROM email_outbox WHERE dedup_key = @key", new NpgsqlParameter("key", "x:patient"));
        var hash = await ExplainAsync("SELECT id FROM patient WHERE identity_number_hash = @hash", new NpgsqlParameter("hash", "abc"));

        AssertUsesIndex(dedup, "ix_email_outbox_dedup_key");
        AssertUsesIndex(hash, "ix_patient_identity_number_hash");
    }

    [Fact]
    public async Task Giris_ve_yenileme_aramalari_benzersiz_indeksle_yapilir()
    {
        var email = await ExplainAsync("SELECT id FROM user_account WHERE email = @email", new NpgsqlParameter("email", "a@b.test"));
        var token = await ExplainAsync("SELECT id FROM refresh_token WHERE token_hash = @hash", new NpgsqlParameter("hash", "abc"));

        AssertUsesIndex(email, "ix_user_account_email");
        AssertUsesIndex(token);
    }

    [Fact]
    public async Task Doktorun_uygunluk_ve_izin_sorgulari_indeksle_yapilir()
    {
        var availability = await ExplainAsync("SELECT id FROM doctor_availability WHERE doctor_id = @doctor AND day_of_week = 1", Uuid("doctor"));
        var timeOff = await ExplainAsync("SELECT id FROM doctor_time_off WHERE doctor_id = @doctor AND start_at >= @from", Uuid("doctor"), Time("from"));

        AssertUsesIndex(availability);
        AssertUsesIndex(timeOff);
    }
}
