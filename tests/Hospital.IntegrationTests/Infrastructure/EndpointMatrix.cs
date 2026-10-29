using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Bir ucun açık yetki politikası (<c>ANON</c> = anonim).</summary>
public sealed record EndpointRule(string Method, string Route, string Policy)
{
    public override string ToString() => $"{Method} {Route} [{Policy}]";
}

/// <summary>
/// Yetki matrisinin <b>elle yazılmış, gözden geçirilmiş</b> kaynağı: her API ucunun politikası burada açıkça yazılıdır.
/// Politikası gevşetilen/değiştirilen uç ya da tabloya eklenmeden eklenen yeni uç testi kırar (yeni uç bilinçli olarak yetkilendirilmeli).
/// </summary>
public static class EndpointMatrix
{
    public const string Anonymous = "ANON";

    public static readonly IReadOnlyList<EndpointRule> Rules =
    [
        // ---- Kimlik
        new("POST", "/api/auth/register", Anonymous),
        new("POST", "/api/auth/login", Anonymous),
        new("POST", "/api/auth/refresh", Anonymous),
        new("POST", "/api/auth/forgot-password", Anonymous),
        new("POST", "/api/auth/reset-password", Anonymous),
        new("POST", "/api/auth/logout", "Authenticated"),
        new("GET", "/api/auth/me", "Authenticated"),
        new("POST", "/api/auth/change-password", "Authenticated"),

        // ---- Anonim katalog
        new("GET", "/api/catalog/hospitals", Anonymous),
        new("GET", "/api/catalog/hospitals/{id}/departments", Anonymous),
        new("GET", "/api/catalog/hospitals/{id}/services", Anonymous),
        new("GET", "/api/catalog/doctors", Anonymous),
        new("GET", "/api/catalog/doctors/{id}/slots", Anonymous),

        // ---- Hasta
        new("GET", "/api/patients/me", "Patient"),
        new("PUT", "/api/patients/me", "Patient"),
        new("GET", "/api/patients/me/appointments", "Patient"),

        // ---- Randevu
        new("POST", "/api/appointments", "Patient"),
        new("GET", "/api/appointments", "Admin"),
        new("GET", "/api/appointments/{id}", "Authenticated"),
        new("POST", "/api/appointments/{id}/approve", "Doctor"),
        new("POST", "/api/appointments/{id}/reject", "Doctor"),
        new("POST", "/api/appointments/{id}/complete", "Doctor"),
        new("POST", "/api/appointments/{id}/cancel", "Authenticated"),

        // ---- Doktor (kendi)
        new("GET", "/api/doctors/me", "Doctor"),
        new("PUT", "/api/doctors/me", "Doctor"),
        new("GET", "/api/doctors/me/appointments", "Doctor"),
        new("GET", "/api/doctors/me/schedule", "Doctor"),
        new("GET", "/api/doctors/me/availability", "Doctor"),
        new("POST", "/api/doctors/me/availability", "Doctor"),
        new("PUT", "/api/doctors/me/availability/{id}", "Doctor"),
        new("DELETE", "/api/doctors/me/availability/{id}", "Doctor"),
        new("GET", "/api/doctors/me/time-off", "Doctor"),
        new("POST", "/api/doctors/me/time-off", "Doctor"),
        new("DELETE", "/api/doctors/me/time-off/{id}", "Doctor"),
        new("GET", "/api/doctors/me/services", "Doctor"),
        new("POST", "/api/doctors/me/services", "Doctor"),
        new("DELETE", "/api/doctors/me/services/{serviceId}", "Doctor"),

        // ---- Yönetici: doktor, personel, yönetici, kullanıcı
        new("GET", "/api/doctors", "Admin"),
        new("POST", "/api/doctors", "Admin"),
        new("GET", "/api/doctors/{id}", "Admin"),
        new("PUT", "/api/doctors/{id}", "Admin"),
        new("PUT", "/api/doctors/{id}/services", "Admin"),
        new("POST", "/api/doctors/{doctorId}/services/{serviceId}/approve", "Admin"),
        new("POST", "/api/doctors/{doctorId}/services/{serviceId}/reject", "Admin"),
        new("DELETE", "/api/doctors/{doctorId}/services/{serviceId}", "Admin"),
        new("GET", "/api/doctor-service-requests", "Admin"),
        new("GET", "/api/staff", "Admin"),
        new("POST", "/api/staff", "Admin"),
        new("GET", "/api/admins", "Admin"),
        new("POST", "/api/admins", "SuperAdmin"),
        new("POST", "/api/users/{id}/deactivate", "Admin"),
        new("POST", "/api/users/{id}/activate", "Admin"),

        // ---- Yönetici: kurum
        new("GET", "/api/organization", "SuperAdmin"),
        new("PUT", "/api/organization", "SuperAdmin"),
        new("GET", "/api/hospitals", "Admin"),
        new("POST", "/api/hospitals", "SuperAdmin"),
        new("GET", "/api/hospitals/{id}", "Admin"),
        new("PUT", "/api/hospitals/{id}", "Admin"),
        new("PUT", "/api/hospitals/{id}/working-hours", "Admin"),
        new("POST", "/api/hospitals/{id}/deactivate", "Admin"),
        new("GET", "/api/hospitals/{hospitalId}/departments", "Admin"),
        new("POST", "/api/hospitals/{hospitalId}/departments", "Admin"),
        new("PUT", "/api/departments/{id}", "Admin"),
        new("POST", "/api/departments/{id}/deactivate", "Admin"),
        new("GET", "/api/services", "Admin"),
        new("POST", "/api/services", "Admin"),
        new("GET", "/api/services/{id}", "Admin"),
        new("PUT", "/api/services/{id}", "Admin"),
        new("POST", "/api/services/{id}/deactivate", "Admin"),
    ];

    /// <summary>Çalışan uygulamadaki gerçek API uçları (rota kısıtları ve sondaki eğik çizgi atılmış).</summary>
    public static IReadOnlyList<EndpointRule> Discover(EndpointDataSource source)
    {
        var found = new List<EndpointRule>();

        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            var route = Normalize(endpoint.RoutePattern.RawText ?? string.Empty);
            if (!route.StartsWith("/api/", StringComparison.Ordinal))
            {
                continue; // /health, /openapi, /scalar altyapı uçlarıdır (ayrıca sınanır)
            }

            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(a => a.Policy)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .Order();
            var policy = anonymous ? Anonymous : string.Join("+", policies);

            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            {
                found.Add(new EndpointRule(method, route, policy));
            }
        }

        return found;
    }

    public static string Normalize(string route)
    {
        var withoutConstraints = Regex.Replace(route, @"\{(\w+)(:[^}]*)?\??\}", "{$1}");
        return withoutConstraints.Length > 1 ? "/" + withoutConstraints.Trim('/') : withoutConstraints;
    }
}
