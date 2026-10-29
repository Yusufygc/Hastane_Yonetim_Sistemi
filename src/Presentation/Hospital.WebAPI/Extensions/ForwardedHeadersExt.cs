using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Hospital.WebAPI.Extensions;

/// <summary>
/// Ters vekil (nginx vb.) arkasında gerçek istemci IP'sini okumak için <c>X-Forwarded-For/Proto</c> desteği.
/// Varsayılan KAPALI: başlık yalnızca <c>ForwardedHeaders:Enabled=true</c> ve istek <b>güvenilen</b> bir vekilden geliyorsa dikkate alınır
/// (<c>ForwardedHeaders:TrustedNetworks</c> CIDR listesi veya <c>TrustedProxies</c> IP listesi); aksi halde istemci başlığı taklit edip hız
/// sınırını atlatabilirdi. Neden gerekli: hız sınırı IP'ye göre bölümlenir; vekil arkasında herkes vekilin IP'sini taşırdı ve tüm kullanıcılar tek
/// kovayı paylaşırdı. Ayrıntı: wiki <c>operasyon</c>, karar K-82.
/// </summary>
public static class ForwardedHeadersExt
{
    public const string SectionName = "ForwardedHeaders";

    public static bool IsEnabled(this IConfiguration configuration) => configuration.GetValue<bool>($"{SectionName}:Enabled");

    /// <summary>Ayar anında (istek/başlangıç) okunur; testler ve dağıtım ortam değişkeniyle değiştirebilsin.</summary>
    public static IServiceCollection AddForwardedHeadersSupport(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            if (!configuration.IsEnabled())
            {
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1; // yalnız bir vekil atlaması; zincirleme taklit denemesi sayılmaz

            // Varsayılan "yalnız loopback güvenilir" listesi temizlenir; yalnız yapılandırılanlar güvenilir.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            var section = configuration.GetSection(SectionName);
            foreach (var cidr in section.GetSection("TrustedNetworks").Get<string[]>() ?? [])
            {
                if (!System.Net.IPNetwork.TryParse(cidr.Trim(), out var network))
                {
                    throw new InvalidOperationException($"{SectionName}:TrustedNetworks geçersiz CIDR içeriyor: '{cidr}' (örn. 172.16.0.0/12).");
                }

                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(network.BaseAddress, network.PrefixLength));
            }

            foreach (var proxy in section.GetSection("TrustedProxies").Get<string[]>() ?? [])
            {
                if (!IPAddress.TryParse(proxy.Trim(), out var address))
                {
                    throw new InvalidOperationException($"{SectionName}:TrustedProxies geçersiz IP içeriyor: '{proxy}'.");
                }

                options.KnownProxies.Add(address);
            }

            // "Herkese güven" güvensiz bir varsayılan olurdu; açıkça kimin vekil olduğu söylenmeli.
            if (options.KnownNetworks.Count == 0 && options.KnownProxies.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Enabled=true iken {SectionName}:TrustedNetworks veya {SectionName}:TrustedProxies verilmelidir (güvenilen vekil adresleri).");
            }
        });

        return services;
    }

    /// <summary>Açıksa en dışta çalışır (korelasyon kimliği, hız sınırı ve loglar gerçek istemci IP'sini görsün).</summary>
    public static WebApplication UseForwardedHeadersIfEnabled(this WebApplication app)
    {
        if (app.Configuration.IsEnabled())
        {
            // Yanlış yapılandırma ilk istekte değil açılışta patlasın.
            _ = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
            app.UseForwardedHeaders();
        }

        return app;
    }
}
