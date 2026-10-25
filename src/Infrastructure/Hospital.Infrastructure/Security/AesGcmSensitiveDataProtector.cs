using System.Security.Cryptography;
using System.Text;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;

namespace Hospital.Infrastructure.Security;

/// <summary>
/// AES-256-GCM ile alan şifreleme + HMAC-SHA256 kör indeks. Ana anahtardan HKDF ile iki ayrı alt anahtar türetilir
/// (şifreleme ve indeks anahtarları birbirinden bağımsızdır). Saklanan biçim: <c>enc:v1:</c> + Base64(nonce[12] ‖ etiket[16] ‖ şifreli metin);
/// amaç etiketi ilişkili veri (AAD) olarak imzalanır. <c>v1</c> öneki ileride anahtar/algoritma değişimini mümkün kılar.
/// Singleton'dır; <see cref="AesGcm"/> örnekleri çağrı başına oluşturulur (iş parçacığı güvenli).
/// </summary>
public sealed class AesGcmSensitiveDataProtector : ISensitiveDataProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _encryptionKey;
    private readonly byte[] _indexKey;

    public AesGcmSensitiveDataProtector(DataProtectionSettings settings)
    {
        var master = settings.ValidatedKey();
        _encryptionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: "hospital:encryption:v1"u8.ToArray());
        _indexKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: "hospital:blind-index:v1"u8.ToArray());
    }

    public string Protect(string plaintext, string purpose)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(purpose));

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string stored, string purpose)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return stored; // şifrelemeden önce yazılmış eski düz metin (yükseltme süreci bunu şifreler)
        }

        var payload = Convert.FromBase64String(stored[Prefix.Length..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Şifreli veri biçimi geçersiz.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(purpose));

        return Encoding.UTF8.GetString(plain);
    }

    public string BlindIndex(string value, string purpose) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_indexKey, Encoding.UTF8.GetBytes($"{purpose}:{value}")));
}
