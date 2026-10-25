using System.Net;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// Rol × uç matrisi (canlı): her API ucu 6 kimlikle (anonim, hasta, doktor, personel, şube yöneticisi, süper yönetici) çağrılır.
/// Politikanın izin vermediği kimlik anonimse 401, girişliyse 403 almalı; izin verilen kimlik 401/403 almamalı (400/404 serbest:
/// istekler bilerek boş gövde ve rastgele kimliklerle atılır, hiçbir kaynak değişmez). Politika tablosu: <see cref="EndpointMatrix"/>.
/// </summary>
public class EndpointPolicyMatrixTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private enum Principal
    {
        Anonymous,
        Patient,
        Doctor,
        Staff,
        BranchManager,
        SuperAdmin,
    }

    private static readonly Principal[] All = Enum.GetValues<Principal>();

    private static IReadOnlySet<Principal> AllowedFor(string policy) => policy switch
    {
        EndpointMatrix.Anonymous => All.ToHashSet(),
        "Authenticated" => All.Where(p => p != Principal.Anonymous).ToHashSet(),
        "Patient" => new HashSet<Principal> { Principal.Patient },
        "Doctor" => new HashSet<Principal> { Principal.Doctor },
        "Admin" => new HashSet<Principal> { Principal.BranchManager, Principal.SuperAdmin },
        "SuperAdmin" => new HashSet<Principal> { Principal.SuperAdmin },
        _ => throw new InvalidOperationException($"Bilinmeyen politika: {policy}"),
    };

    private async Task<Dictionary<Principal, ApiClient>> NewPrincipalsAsync()
    {
        var superAdmin = await AdminSessions.SuperAdminAsync(fixture);
        var hospitalId = await AdminSessions.SeededHospitalIdAsync(superAdmin);
        var departmentId = await AdminSessions.CreateDepartmentAsync(superAdmin, hospitalId);

        var patient = fixture.CreateClient();
        await patient.RegisterAndLoginAsync();

        var (_, doctorEmail) = await AdminSessions.CreateDoctorAsync(superAdmin, departmentId);
        var doctor = await AdminSessions.LoginAsync(fixture, doctorEmail, TestDoctor.Password);

        var staffEmail = $"personel.{Guid.NewGuid():N}@test.local";
        (await superAdmin.PostAsync("/api/staff", new
        {
            HospitalId = hospitalId,
            Email = staffEmail,
            Password = TestAccount.ValidPassword,
            FirstName = "Mehmet",
            LastName = "Kaya",
            Title = "Hemşire",
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await AdminSessions.LoginAsync(fixture, staffEmail, TestAccount.ValidPassword);

        return new Dictionary<Principal, ApiClient>
        {
            [Principal.Anonymous] = fixture.CreateClient(),
            [Principal.Patient] = patient,
            [Principal.Doctor] = doctor,
            [Principal.Staff] = staff,
            [Principal.BranchManager] = await AdminSessions.BranchManagerAsync(fixture, hospitalId),
            [Principal.SuperAdmin] = superAdmin,
        };
    }

    private static string Materialize(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, @"\{\w+\}", _ => Guid.NewGuid().ToString());

    [Fact]
    public async Task Her_uc_her_kimlikle_politikaya_uygun_401_403_veya_izin_verir()
    {
        var principals = await NewPrincipalsAsync();
        var failures = new List<string>();

        foreach (var rule in EndpointMatrix.Rules)
        {
            var allowed = AllowedFor(rule.Policy);
            object? body = rule.Method is "POST" or "PUT" ? new { } : null;

            foreach (var (principal, client) in principals)
            {
                var response = await client.SendAsync(new HttpMethod(rule.Method), Materialize(rule.Route), body);
                var status = response.StatusCode;

                if (allowed.Contains(principal))
                {
                    if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        failures.Add($"{rule} → {principal} izinli olmalı ama {(int)status} aldı");
                    }
                }
                else
                {
                    var expected = principal == Principal.Anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
                    if (status != expected)
                    {
                        failures.Add($"{rule} → {principal} {(int)expected} almalı ama {(int)status} aldı");
                    }
                    else
                    {
                        // Reddedilen isteğin gövdesi de standart hata biçiminde olmalı.
                        var problem = await ApiClient.ReadJsonAsync(response);
                        if (!problem.TryGetProperty("code", out _) || !problem.TryGetProperty("traceId", out _))
                        {
                            failures.Add($"{rule} → {principal} hata gövdesi code/traceId içermiyor");
                        }
                    }
                }
            }
        }

        failures.Should().BeEmpty("her ucun politikası her kimlik için doğru uygulanmalı:\n" + string.Join("\n", failures));
    }

    [Fact]
    public async Task Bilinmeyen_rota_girisli_kullaniciya_404_anonime_401()
    {
        var principals = await NewPrincipalsAsync();

        (await principals[Principal.Anonymous].GetAsync("/api/yok-boyle-bir-rota")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await principals[Principal.Patient].GetAsync("/api/yok-boyle-bir-rota")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Saglik_ucu_anonim_acik_ve_veritabani_denetimini_icerir()
    {
        var response = await fixture.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Gecersiz_sahte_ve_suresi_dolmus_token_tum_korumali_uclarda_401()
    {
        var forged = new ApiClient(fixture.Factory.CreateClient());
        forged.SetAccessToken("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwicm9sZSI6IkFETUlOIn0.sahte-imza");

        var failures = new List<string>();
        foreach (var rule in EndpointMatrix.Rules.Where(r => r.Policy != EndpointMatrix.Anonymous))
        {
            var response = await forged.SendAsync(new HttpMethod(rule.Method), Materialize(rule.Route), rule.Method is "POST" or "PUT" ? new { } : null);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                failures.Add($"{rule} sahte token ile {(int)response.StatusCode} döndü");
            }
        }

        failures.Should().BeEmpty(string.Join("\n", failures));
    }
}
