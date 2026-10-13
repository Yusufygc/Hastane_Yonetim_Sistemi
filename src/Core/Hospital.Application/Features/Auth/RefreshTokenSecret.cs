using System.Security.Cryptography;
using System.Text;

namespace Hospital.Application.Features.Auth;

/// <summary>Yenileme belirteci üretimi ve özetleme. Düz değer yalnızca istemciye verilir, veritabanında özeti durur.</summary>
public static class RefreshTokenSecret
{
    private const int SizeInBytes = 32; // 256 bit

    /// <summary>Kriptografik rastgele, URL'de güvenli (base64url) yenileme belirteci.</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(SizeInBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>SHA-256 özeti (küçük harf hex). Yüksek entropili rastgele değer için tuzlu yavaş özet gerekmez.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
