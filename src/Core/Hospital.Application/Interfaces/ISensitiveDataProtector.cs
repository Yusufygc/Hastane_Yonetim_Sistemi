namespace Hospital.Application.Interfaces;

/// <summary>
/// Hassas kişisel verinin (KVKK; ör. T.C. kimlik no) veritabanında şifreli saklanması ve şifre çözülmeden aranabilmesi.
/// <c>purpose</c> (amaç) şifreli değeri ve kör indeksi bir alana bağlar: bir alanın şifreli değeri başka alana kopyalanıp çözülemez.
/// </summary>
public interface ISensitiveDataProtector
{
    /// <summary>Kimliği doğrulanmış şifreleme (AES-256-GCM); her çağrıda farklı çıktı üretir (rastgele nonce).</summary>
    string Protect(string plaintext, string purpose);

    /// <summary>
    /// Şifreyi çözer. <c>enc:</c> önekli olmayan değer, şifrelemeden önce yazılmış <b>eski düz metin</b> kabul edilip olduğu gibi
    /// döner (yükseltme sürecinde okunabilsin diye). Yanlış anahtar veya değiştirilmiş veri istisna fırlatır.
    /// </summary>
    string Unprotect(string stored, string purpose);

    /// <summary>
    /// Aynı girdiye hep aynı çıktıyı veren anahtarlı özet (HMAC-SHA256, 64 onaltılık karakter): şifreli alanda eşitlik/benzersizlik
    /// aramak için. Anahtarsız tahmin edilemez; özetten girdiye dönülemez.
    /// </summary>
    string BlindIndex(string value, string purpose);
}

/// <summary>Şifrelenen alanların amaç etiketleri (şifreleme ve kör indeks için ortak).</summary>
public static class SensitiveDataPurposes
{
    public const string PatientIdentityNumber = "patient.identity_number";
}
