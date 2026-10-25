using System.Net;
using AwesomeAssertions;
using Hospital.Application.Interfaces;
using Hospital.IntegrationTests.Infrastructure;
using Hospital.Persistence.Context;
using Hospital.Persistence.DataUpgrades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Hospital.IntegrationTests;

/// <summary>
/// KVKK: T.C. kimlik no veritabanında şifreli (AES-GCM) saklanır, arama/benzersizlik kör indeksle (HMAC) yapılır;
/// API davranışı (maskeli yanıt, tekrar kayıt reddi) değişmez; eski düz metin kayıtlar yükseltilir.
/// </summary>
public class IdentityNumberEncryptionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(string Stored, string? Hash)> RawColumnsAsync(string email)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT p.identity_number, p.identity_number_hash FROM patient p JOIN user_account u ON u.id = p.id WHERE u.email = @email",
            connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    [Fact]
    public async Task Kayit_sonrasi_veritabaninda_duz_TC_yoktur_sifreli_ve_kor_indeksli()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        var (stored, hash) = await RawColumnsAsync(account.Email);

        stored.Should().StartWith("enc:v1:").And.NotContain(account.IdentityNumber);
        hash.Should().MatchRegex("^[0-9a-f]{64}$").And.NotContain(account.IdentityNumber);
    }

    [Fact]
    public async Task Ayni_TC_iki_kez_kaydedilemez_409_sifreli_olsa_da()
    {
        var first = TestAccount.New();
        var second = TestAccount.New() with { IdentityNumber = first.IdentityNumber };
        var client = fixture.CreateClient();
        (await client.PostAsync("/api/auth/register", first.RegistrationBody())).StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await client.PostAsync("/api/auth/register", second.RegistrationBody());

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiClient.ReadJsonAsync(duplicate)).GetProperty("code").GetString().Should().Be("auth.identity_number_taken");
    }

    [Fact]
    public async Task Farkli_hastalarin_sifreli_metin_ve_kor_indeksleri_farklidir()
    {
        var client = fixture.CreateClient();
        var a = await client.RegisterAndLoginAsync();
        var b = await fixture.CreateClient().RegisterAndLoginAsync();

        var (storedA, hashA) = await RawColumnsAsync(a.Email);
        var (storedB, hashB) = await RawColumnsAsync(b.Email);

        storedA.Should().NotBe(storedB);
        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public async Task Profil_maskeli_TC_sifreleme_arka_planda_seffaf_calisir()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        var profile = await ApiClient.ReadJsonAsync(await client.GetAsync("/api/patients/me"));

        profile.GetProperty("maskedIdentityNumber").GetString()
            .Should().Be($"{account.IdentityNumber[..3]}******{account.IdentityNumber[^2..]}");
    }

    [Fact]
    public async Task Profil_guncellemesi_sifreli_TC_yi_bozmaz_indeks_ayni_kalir()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();
        var (_, hashBefore) = await RawColumnsAsync(account.Email);

        var update = await client.PutAsync("/api/patients/me", new { FirstName = "Fatma", LastName = "Demir", Phone = "05550000000", BloodType = "0+" });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var (storedAfter, hashAfter) = await RawColumnsAsync(account.Email);
        storedAfter.Should().StartWith("enc:v1:");
        hashAfter.Should().Be(hashBefore);
        (await ApiClient.ReadJsonAsync(await client.GetAsync("/api/patients/me"))).GetProperty("maskedIdentityNumber").GetString()
            .Should().StartWith(account.IdentityNumber[..3]);
    }

    [Fact]
    public async Task Eski_duz_metin_kayit_yukseltilir_sifrelenir_kor_indeks_yazilir_ve_tekrar_kayit_engellenir()
    {
        var legacy = TestAccount.New();
        var client = fixture.CreateClient();
        (await client.PostAsync("/api/auth/register", legacy.RegistrationBody())).StatusCode.Should().Be(HttpStatusCode.Created);

        // Şifrelemeden önceki durumu taklit et: düz metin TC, boş kör indeks.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE patient SET identity_number = {0}, identity_number_hash = NULL FROM user_account u WHERE u.id = patient.id AND u.email = {1}",
                legacy.IdentityNumber, legacy.Email);
        }

        (await RawColumnsAsync(legacy.Email)).Should().Be((legacy.IdentityNumber, (string?)null));

        // Eski kayıt hâlâ okunabilir (düz metin geçişi) ve profil maskeli döner.
        await client.LoginAsync(legacy.Email, legacy.Password);
        (await ApiClient.ReadJsonAsync(await client.GetAsync("/api/patients/me"))).GetProperty("maskedIdentityNumber").GetString()
            .Should().StartWith(legacy.IdentityNumber[..3]);

        int upgraded;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            upgraded = await scope.ServiceProvider.GetRequiredService<IdentityNumberUpgrader>().UpgradeAsync();
        }

        upgraded.Should().BeGreaterThanOrEqualTo(1);
        var (stored, hash) = await RawColumnsAsync(legacy.Email);
        stored.Should().StartWith("enc:v1:").And.NotContain(legacy.IdentityNumber);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");

        // Yükseltme sonrası: aynı TC ile yeni kayıt reddedilir, yükseltme idempotenttir, kullanıcı hâlâ giriş yapar.
        var duplicate = await fixture.CreateClient().PostAsync("/api/auth/register", (TestAccount.New() with { IdentityNumber = legacy.IdentityNumber }).RegistrationBody());
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IdentityNumberUpgrader>().UpgradeAsync()).Should().Be(0);
        }

        await fixture.CreateClient().LoginAsync(legacy.Email, legacy.Password);
        (await ApiClient.ReadJsonAsync(await client.GetAsync("/api/patients/me"))).GetProperty("maskedIdentityNumber").GetString()
            .Should().StartWith(legacy.IdentityNumber[..3]);
    }

    [Fact]
    public async Task Veritabanina_dogrudan_yazilan_sifreli_metin_degistirilirse_okuma_acik_hata_verir()
    {
        var client = fixture.CreateClient();
        var account = await client.RegisterAndLoginAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = (await RawColumnsAsync(account.Email)).Stored;
            var tampered = stored[..^4] + (stored.EndsWith("AAAA") ? "BBBB" : "AAAA"); // son 4 karakter değiştirilir
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE patient SET identity_number = {0} FROM user_account u WHERE u.id = patient.id AND u.email = {1}", tampered, account.Email);
        }

        var response = await client.GetAsync("/api/patients/me");

        // Sessizce yanlış veri dönmez; hata (500) olur ve ayrıntı sızdırılmaz.
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(account.IdentityNumber);
    }

    [Fact]
    public async Task Anahtar_eksik_veya_hatali_ise_uygulama_baslarken_acik_hata_verir()
    {
        foreach (var key in new[] { "", "base64-degil!!", Convert.ToBase64String(new byte[16]) })
        {
            await using var factory = fixture.CreateFactory(new Dictionary<string, string?> { ["Security:DataProtection:Key"] = key });

            var act = () => factory.CreateClient();

            act.Should().Throw<InvalidOperationException>().Which.ToString()
                .Should().Match(m => m.Contains("Security:DataProtection:Key"));
        }
    }

    [Fact]
    public async Task Protector_tek_orneklidir_EF_modeli_ayni_anahtari_paylasir()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var first = scope.ServiceProvider.GetRequiredService<ISensitiveDataProtector>();
        await using var other = fixture.Factory.Services.CreateAsyncScope();

        other.ServiceProvider.GetRequiredService<ISensitiveDataProtector>().Should().BeSameAs(first);
    }
}
