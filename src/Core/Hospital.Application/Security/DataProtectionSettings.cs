namespace Hospital.Application.Security;

/// <summary>
/// Hassas veri şifreleme anahtarı (<c>Security:DataProtection:Key</c>): Base64, tam 32 bayt. Üretimde ortam değişkeni/gizli
/// depo ile verilir (<c>Security__DataProtection__Key</c>); koda/depoya konmaz. Eksik veya hatalıysa uygulama başlamaz.
/// <b>Anahtar kaybolursa şifreli veri çözülemez; yedekleyin. Anahtar değişirse mevcut şifreli veri okunamaz.</b>
/// </summary>
public sealed record DataProtectionSettings(string Key)
{
    public const int KeyBytes = 32;

    public byte[] ValidatedKey()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException(
                "Security:DataProtection:Key ayarı zorunludur (ortam değişkeni: Security__DataProtection__Key). " +
                "Üretmek için: openssl rand -base64 32");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(Key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Security:DataProtection:Key geçerli bir Base64 değeri olmalıdır (openssl rand -base64 32).");
        }

        return bytes.Length == KeyBytes
            ? bytes
            : throw new InvalidOperationException($"Security:DataProtection:Key tam {KeyBytes} bayt (Base64'te 44 karakter) olmalıdır; verilen {bytes.Length} bayt.");
    }
}
