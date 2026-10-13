using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Hospital.WebAPI.Extensions;

/// <summary>
/// OpenAPI dokümanına Bearer (JWT) şemasını ekler ve yetki gerektiren uçları işaretler; Scalar'da
/// "Authorize" düğmesi çıkar ve kilitli uçlarda gereken politika açıklamada görünür.
/// </summary>
public static class OpenApiExt
{
    private const string SchemeId = "Bearer";

    public static IServiceCollection AddOpenApiWithBearer(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSchemeTransformer>();
            options.AddOperationTransformer<SecuredOperationTransformer>();
        });

    private sealed class BearerSchemeTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Info.Title = "Hastane / Klinik Randevu Sistemi API";
            document.Info.Description =
                "Kimlik doğrulama JWT Bearer iledir. 1) `POST /api/auth/register` ile hasta kaydı, 2) `POST /api/auth/login` ile giriş, " +
                "3) yanıttaki `accessToken` değerini sağ üstteki **Authorize** (Bearer) alanına yapıştırın. " +
                "Erişim belirteci kısa ömürlüdür; süresi dolunca `POST /api/auth/refresh` ile `refreshToken` kullanarak yenileyin.";

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

            return Task.CompletedTask;
        }
    }

    private sealed class SecuredOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;

            if (metadata.OfType<IAllowAnonymous>().Any())
            {
                return Task.CompletedTask;
            }

            var authorize = metadata.OfType<IAuthorizeData>().ToList();
            if (authorize.Count == 0)
            {
                return Task.CompletedTask;
            }

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeId } }] = [],
            });

            var policies = authorize.Select(a => a.Policy).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            var note = policies.Count > 0
                ? $"**Yetki:** `{string.Join("`, `", policies)}` politikası (Bearer gerekir). Rol yetersizse 403, kapsam dışı kaynakta 404."
                : "**Yetki:** giriş yapmış kullanıcı (Bearer gerekir).";

            operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? note : $"{operation.Description}\n\n{note}";
            return Task.CompletedTask;
        }
    }
}
