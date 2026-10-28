using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Hospital.WebAPI.Extensions;

/// <summary>
/// OpenAPI belgesi (API sözleşmesi): Bearer (JWT) şeması, her uç için Türkçe özet, gereken yetki politikası, olası hata yanıtları
/// (standart <c>ProblemDetails</c> gövdesiyle) ve sayfalama başlıkları. Scalar'da "Authorize" düğmesi çıkar.
/// Belge <c>docs/api/openapi.v1.json</c> dosyasında dondurulur; sözleşme değişikliği testte (<c>OpenApiContractTests</c>) fark edilir.
/// </summary>
public static class OpenApiExt
{
    private const string SchemeId = "Bearer";
    private const string ProblemSchemaId = "ProblemDetails";
    private const string ProblemContentType = "application/problem+json";

    public static IServiceCollection AddOpenApiWithBearer(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<ApiDocumentTransformer>();
            options.AddOperationTransformer<ApiOperationTransformer>();
        });

    private sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Info.Title = "Hastane / Klinik Randevu Sistemi API";
            document.Info.Version = "v1";
            document.Info.Description =
                """
                Çok şubeli (veya tek şubeli) hastane/klinik randevu yönetim API'si.

                **Kimlik doğrulama (JWT Bearer):** 1) `POST /api/auth/register` ile hasta kaydı, 2) `POST /api/auth/login` ile giriş,
                3) yanıttaki `accessToken` değerini sağ üstteki **Authorize** (Bearer) alanına yapıştırın. Erişim belirteci kısa ömürlüdür;
                süresi dolunca `POST /api/auth/refresh` ile `refreshToken` kullanarak yenileyin. Rol URL'de değil token'dadır:
                `PATIENT`, `DOCTOR`, `STAFF`, `ADMIN` (`SUPER_ADMIN` / `BRANCH_MANAGER`).

                **Hata biçimi:** tüm hatalar `application/problem+json` (ProblemDetails): Türkçe `title`, makine okunur `code`, `traceId`,
                doğrulama hatalarında alan bazlı `errors` (alan adları camelCase). 401 kimlik yok/geçersiz, 403 rol yetersiz,
                404 kaynak yok **veya sahibi/kapsamı değilsiniz**, 409 çakışma, 429 hız sınırı.

                **İzleme:** her yanıtta `X-Correlation-Id` başlığı döner (istemci geçerli bir değer gönderirse korunur); hata yanıtlarındaki
                `traceId` aynı değerdir. Şikâyet bildirirken bu kimliği iletin.

                **Sayfalama:** liste uçları `page` (1'den başlar) ve `pageSize` (1–200, varsayılan 50) alır; gövde yalnız kayıt dizisidir,
                toplam ve sayfa bilgisi `X-Total-Count`, `X-Page`, `X-Page-Size` yanıt başlıklarındadır.

                **Biçimler:** tarih `yyyy-MM-dd`, saat `HH:mm`, zaman damgası ISO 8601 (randevu zamanları UTC). Gün: `0` = Pazar … `6` = Cumartesi.
                """.ReplaceLineEndings("\n"); // ham metin dosyanın satır sonlarını taşır (Windows checkout: CRLF); sözleşme her ortamda aynı olsun

            // Sunucu adresi isteğe göre değişir; belge dağıtımdan bağımsız ve kararlı kalsın diye yazılmaz.
            document.Servers = [];

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Giriş yanıtındaki erişim belirteci (accessToken). 'Bearer ' önekini yazmayın.",
            };

            document.Components.Schemas ??= new Dictionary<string, OpenApiSchema>();
            document.Components.Schemas[ProblemSchemaId] = BuildProblemSchema();

            return Task.CompletedTask;
        }

        private static OpenApiSchema BuildProblemSchema() => new()
        {
            Type = "object",
            Description = "Standart hata gövdesi (RFC 9457 ProblemDetails + code, traceId, errors).",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["type"] = new() { Type = "string", Nullable = true },
                ["title"] = new() { Type = "string", Description = "Türkçe kısa başlık." },
                ["status"] = new() { Type = "integer", Format = "int32" },
                ["detail"] = new() { Type = "string", Nullable = true, Description = "Okunabilir açıklama." },
                ["instance"] = new() { Type = "string", Nullable = true },
                ["code"] = new() { Type = "string", Description = "Makine okunur hata kodu (ör. appointment.slot_taken)." },
                ["traceId"] = new() { Type = "string", Description = "Yanıttaki X-Correlation-Id ile aynı izleme kimliği." },
                ["errors"] = new()
                {
                    Type = "object",
                    Nullable = true,
                    Description = "Doğrulama hatalarında alan adı → mesaj listesi.",
                    AdditionalProperties = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "string" } },
                },
            },
        };
    }

    private sealed class ApiOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var method = context.Description.HttpMethod ?? string.Empty;
            var path = "/" + (context.Description.RelativePath ?? string.Empty).Split('?')[0];

            operation.Summary ??= OpenApiSummaries.Find(method, path);

            var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            var authorize = metadata.OfType<IAuthorizeData>().ToList();
            var policies = authorize.Select(a => a.Policy).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Order().ToList();
            var protectedOperation = !allowsAnonymous && authorize.Count > 0;

            if (protectedOperation)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeId } }] = [],
                });

                var note = policies.Count > 0
                    ? $"**Yetki:** `{string.Join("`, `", policies)}` politikası (Bearer gerekir). Rol yetersizse 403, kapsam/sahiplik dışı kaynakta 404."
                    : "**Yetki:** giriş yapmış kullanıcı (Bearer gerekir).";

                operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? note : $"{operation.Description}\n\n{note}";
            }
            else if (allowsAnonymous)
            {
                var note = "**Yetki:** anonim (giriş gerekmez).";
                operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? note : $"{operation.Description}\n\n{note}";
            }

            var parameters = operation.Parameters ?? [];
            operation.Responses ??= [];

            var hasInput = operation.RequestBody is not null || parameters.Count > 0;
            var hasResourceKey = parameters.Any(p => p.In == ParameterLocation.Path);
            var rateLimited = metadata.OfType<EnableRateLimitingAttribute>().Any();

            if (hasInput)
            {
                AddProblem(operation, "400", "Geçersiz istek / doğrulama hatası (alan bazlı `errors`).");
            }

            if (protectedOperation)
            {
                AddProblem(operation, "401", "Token yok, geçersiz veya süresi dolmuş.");

                if (policies.Any(p => p != "Authenticated"))
                {
                    AddProblem(operation, "403", "Rol bu işlem için yetersiz.");
                }
            }

            if (hasResourceKey)
            {
                AddProblem(operation, "404", "Kaynak yok veya sahibi/kapsamı değilsiniz.");
            }

            if (rateLimited)
            {
                AddProblem(operation, "429", "Hız sınırı aşıldı; bir süre bekleyip tekrar deneyin.");
            }

            if (parameters.Any(p => p.Name == "page") && operation.Responses.TryGetValue("200", out var ok))
            {
                ok.Headers ??= new Dictionary<string, OpenApiHeader>();
                ok.Headers["X-Total-Count"] = IntHeader("Filtreye uyan toplam kayıt sayısı.");
                ok.Headers["X-Page"] = IntHeader("Dönen sayfa numarası.");
                ok.Headers["X-Page-Size"] = IntHeader("Sayfa boyutu.");
            }

            return Task.CompletedTask;
        }

        private static void AddProblem(OpenApiOperation operation, string status, string description)
        {
            if (operation.Responses.ContainsKey(status))
            {
                return;
            }

            operation.Responses[status] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [ProblemContentType] = new()
                    {
                        Schema = new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = ProblemSchemaId } },
                    },
                },
            };
        }

        private static OpenApiHeader IntHeader(string description) => new()
        {
            Description = description,
            Schema = new OpenApiSchema { Type = "integer", Format = "int32", Example = new OpenApiInteger(1) },
        };
    }
}
