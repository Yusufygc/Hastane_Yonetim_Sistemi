using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Hospital.IntegrationTests.Infrastructure;

namespace Hospital.IntegrationTests;

/// <summary>
/// API sözleşmesi (OpenAPI): belge eksiksizdir (her uç özet, yetki notu ve hata yanıtlarıyla) ve <c>docs/api/openapi.v1.json</c>
/// dosyasında <b>dondurulmuştur</b>; üretilen belge dosyadan farklıysa test kırılır (sözleşme bilinçsizce değişemez).
/// Veritabanı gerektirmez. Bilinçli değişiklikten sonra dosyayı <c>scripts/update-openapi-snapshot.ps1</c> ile güncelleyin.
/// </summary>
public class OpenApiContractTests(DbLessApiFixture fixture) : IClassFixture<DbLessApiFixture>
{
    private const string SnapshotRelativePath = "docs/api/openapi.v1.json";

    private async Task<JsonNode> GenerateAsync()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"OpenAPI belgesi üretilebilmeli, yanıt: {(int)response.StatusCode} {body[..Math.Min(body.Length, 1500)]}");
        return JsonNode.Parse(body)!;
    }

    private static IEnumerable<(string Method, string Path, JsonObject Operation)> Operations(JsonNode document)
    {
        foreach (var (path, item) in document["paths"]!.AsObject())
        {
            foreach (var (method, operation) in item!.AsObject())
            {
                yield return (method.ToUpperInvariant(), path, operation!.AsObject());
            }
        }
    }

    [Fact]
    public async Task Belge_her_api_ucunu_icerir_ve_matrisle_ayni_yol_kumesine_sahiptir()
    {
        var document = await GenerateAsync();

        var documented = Operations(document).Select(o => $"{o.Method} {o.Path}").Order().ToList();
        var expected = EndpointMatrix.Rules.Select(r => $"{r.Method} {r.Route}").Order().ToList();

        documented.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering(), "OpenAPI belgesi tüm API uçlarını (ve yalnız onları) içermeli");
    }

    [Fact]
    public async Task Her_ucun_ozeti_etiketi_ve_yetki_notu_vardir()
    {
        var document = await GenerateAsync();
        var failures = new List<string>();

        foreach (var (method, path, operation) in Operations(document))
        {
            if (string.IsNullOrWhiteSpace(operation["summary"]?.GetValue<string>()))
            {
                failures.Add($"{method} {path}: Türkçe özet yok (OpenApiSummaries'e ekleyin)");
            }

            if (operation["tags"]?.AsArray().Count is null or 0)
            {
                failures.Add($"{method} {path}: etiket (tag) yok");
            }

            if (operation["description"]?.GetValue<string>()?.Contains("**Yetki:**") != true)
            {
                failures.Add($"{method} {path}: açıklamada yetki notu yok");
            }
        }

        failures.Should().BeEmpty(string.Join("\n", failures));
    }

    [Fact]
    public async Task Guvenlik_gereksinimi_yalniz_anonim_olmayan_uclarda_vardir()
    {
        var document = await GenerateAsync();
        var policyByRoute = EndpointMatrix.Rules.ToDictionary(r => $"{r.Method} {r.Route}", r => r.Policy);
        var failures = new List<string>();

        foreach (var (method, path, operation) in Operations(document))
        {
            var secured = operation["security"]?.AsArray().Count > 0;
            var anonymous = policyByRoute[$"{method} {path}"] == EndpointMatrix.Anonymous;

            if (secured == anonymous)
            {
                failures.Add($"{method} {path}: güvenlik gereksinimi {(secured ? "var" : "yok")} ama politika {policyByRoute[$"{method} {path}"]}");
            }
        }

        failures.Should().BeEmpty(string.Join("\n", failures));
    }

    [Fact]
    public async Task Hata_yanitlari_standart_ProblemDetails_semasina_baglidir()
    {
        var document = await GenerateAsync();
        var failures = new List<string>();

        foreach (var (method, path, operation) in Operations(document))
        {
            var policy = EndpointMatrix.Rules.Single(r => r.Method == method && r.Route == path).Policy;
            var responses = operation["responses"]!.AsObject();

            if (policy != EndpointMatrix.Anonymous && !responses.ContainsKey("401"))
            {
                failures.Add($"{method} {path}: korumalı uçta 401 yanıtı belgelenmemiş");
            }

            if (policy is "Patient" or "Doctor" or "Admin" or "SuperAdmin" && !responses.ContainsKey("403"))
            {
                failures.Add($"{method} {path}: rol politikalı uçta 403 yanıtı belgelenmemiş");
            }

            if (path.Contains('{') && !responses.ContainsKey("404"))
            {
                failures.Add($"{method} {path}: kaynak kimlikli uçta 404 yanıtı belgelenmemiş");
            }

            foreach (var (status, response) in responses)
            {
                if (status is "400" or "401" or "403" or "404" or "429")
                {
                    var schemaRef = response!["content"]?["application/problem+json"]?["schema"]?["$ref"]?.GetValue<string>();
                    if (schemaRef != "#/components/schemas/ProblemDetails")
                    {
                        failures.Add($"{method} {path}: {status} yanıtı ProblemDetails şemasına bağlı değil");
                    }
                }
            }
        }

        failures.Should().BeEmpty(string.Join("\n", failures));
    }

    private static readonly HashSet<string> Created =
    [
        "POST /api/auth/register", "POST /api/admins", "POST /api/appointments", "POST /api/doctors", "POST /api/doctors/me/availability",
        "POST /api/doctors/me/time-off", "POST /api/hospitals", "POST /api/hospitals/{hospitalId}/departments", "POST /api/services", "POST /api/staff",
    ];

    private static readonly HashSet<string> OkWithBodyDespiteWrite =
        ["POST /api/auth/login", "POST /api/auth/refresh", "PUT /api/patients/me"];

    [Fact]
    public async Task Her_ucun_basari_yaniti_dogru_kodla_ve_govde_semasiyla_belgelenmistir()
    {
        var document = await GenerateAsync();
        var failures = new List<string>();

        foreach (var (method, path, operation) in Operations(document))
        {
            var key = $"{method} {path}";
            var responses = operation["responses"]!.AsObject();
            var success = responses.Where(r => r.Key.StartsWith('2')).Select(r => r.Key).ToList();

            var expected = Created.Contains(key) ? "201"
                : key is "POST /api/doctors/me/services" or "POST /api/auth/forgot-password" ? "202"
                : method == "GET" || OkWithBodyDespiteWrite.Contains(key) ? "200"
                : "204";

            if (!success.SequenceEqual([expected]))
            {
                failures.Add($"{key}: başarı yanıtı [{string.Join(",", success)}], beklenen [{expected}]");
                continue;
            }

            var hasSchema = responses[expected]!["content"]?["application/json"]?["schema"] is not null;
            if (expected is "200" or "201" && !hasSchema)
            {
                failures.Add($"{key}: {expected} yanıtının gövde şeması belgelenmemiş");
            }

            if (expected is "202" or "204" && responses[expected]!["content"] is not null)
            {
                failures.Add($"{key}: {expected} yanıtı gövdesiz olmalı");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Govde_semalari_DTO_alanlarini_icerir_ornek_olarak_randevu_ve_slotlar()
    {
        var document = await GenerateAsync();
        var schemas = document["components"]!["schemas"]!.AsObject();

        schemas["AppointmentDto"]!["properties"]!.AsObject().Select(p => p.Key)
            .Should().Contain(["id", "hospitalId", "patientName", "doctorName", "serviceName", "startAt", "endAt", "status", "decisionReason"]);
        schemas["AvailableSlotsDto"]!["properties"]!.AsObject().Select(p => p.Key).Should().Contain(["timeZoneId", "durationMinutes", "slots"]);
        schemas["AuthTokens"]!["properties"]!.AsObject().Select(p => p.Key).Should().Contain(["accessToken", "refreshToken", "accessTokenExpiresAt"]);
        schemas["CreatedResponse"]!["properties"]!.AsObject().Select(p => p.Key).Should().Contain("id");
    }

    [Fact]
    public async Task Hiz_sinirli_kimlik_uclari_429_belgeler_sayfali_uclar_sayfalama_basliklarini()
    {
        var document = await GenerateAsync();
        var operations = Operations(document).ToDictionary(o => $"{o.Method} {o.Path}", o => o.Operation);

        foreach (var key in new[] { "POST /api/auth/register", "POST /api/auth/login", "POST /api/auth/refresh", "POST /api/auth/change-password" })
        {
            operations[key]["responses"]!.AsObject().ContainsKey("429").Should().BeTrue($"{key} hız sınırlıdır");
        }

        foreach (var key in new[]
                 {
                     "GET /api/appointments", "GET /api/patients/me/appointments", "GET /api/doctors/me/appointments",
                     "GET /api/doctors", "GET /api/staff", "GET /api/doctor-service-requests",
                 })
        {
            var headers = operations[key]["responses"]!["200"]!["headers"]!.AsObject();
            headers.Select(h => h.Key).Should().Contain(["X-Total-Count", "X-Page", "X-Page-Size"], $"{key} sayfalıdır");
        }
    }

    [Fact]
    public async Task Belge_bilgisi_guvenlik_semasi_ve_ortak_semalar_dogru()
    {
        var document = await GenerateAsync();

        document["info"]!["title"]!.GetValue<string>().Should().Contain("Randevu");
        document["info"]!["version"]!.GetValue<string>().Should().Be("v1");
        document["info"]!["description"]!.GetValue<string>().Should().Contain("X-Correlation-Id").And.Contain("X-Total-Count").And.Contain("Bearer");
        document["servers"].Should().BeNull("sunucu adresi dağıtıma göre değişir; belgeye yazılmaz");

        var bearer = document["components"]!["securitySchemes"]!["Bearer"]!;
        bearer["scheme"]!.GetValue<string>().Should().Be("bearer");
        bearer["bearerFormat"]!.GetValue<string>().Should().Be("JWT");

        var problem = document["components"]!["schemas"]!["ProblemDetails"]!["properties"]!.AsObject();
        problem.Select(p => p.Key).Should().Contain(["title", "status", "code", "traceId", "errors"]);
    }

    [Fact]
    public async Task Sozlesme_dondurulmus_dosyayla_ayni()
    {
        var document = await GenerateAsync();
        var generated = Canonicalize(document);

        var update = Environment.GetEnvironmentVariable("OPENAPI_SNAPSHOT_UPDATE") == "1";
        var path = Environment.GetEnvironmentVariable("OPENAPI_SNAPSHOT_PATH") ?? Path.Combine(RepositoryRoot(), SnapshotRelativePath);

        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue($"dondurulmuş sözleşme dosyası bulunamadı: {SnapshotRelativePath}. scripts/update-openapi-snapshot.ps1 ile üretin.");

        var frozen = (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n");

        generated.ReplaceLineEndings("\n").Should().Be(
            frozen,
            "API sözleşmesi dondurulmuş dosyadan farklı. Değişiklik bilinçliyse scripts/update-openapi-snapshot.ps1 ile dosyayı güncelleyip farkı inceleyin ve commit edin; " +
            "değilse yanlışlıkla sözleşme bozulmuştur (yol, parametre, yanıt, şema veya yetki).");
    }

    /// <summary>Anahtarları sıralı, girintili, satır sonu LF: kararlı ve gözle karşılaştırılabilir çıktı.</summary>
    private static string Canonicalize(JsonNode node)
    {
        var sorted = Sort(node)!;
        return sorted.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
                   .ReplaceLineEndings("\n") + "\n";
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
        null => null,
        _ => node.DeepClone(),
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hospital.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Hospital.sln bulunamadı (depo kökü).");
    }
}
