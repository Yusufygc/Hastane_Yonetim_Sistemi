using Hospital.Domain.Entities.Notifications;

namespace Hospital.Application.Features.Notifications;

/// <summary>
/// Parola sıfırlama e-posta şablonları (Türkçe, düz metin). Sıfırlama bağlantısı belirteç içerir; bu yüzden metin
/// "bağlantıyı kimseyle paylaşmayın" uyarısı taşır. Parolanın kendisi hiçbir e-postada yer almaz.
/// </summary>
public static class PasswordEmails
{
    public static EmailContent ResetRequested(string fullName, string link, TimeSpan lifetime) => new(
        "password-reset-requested",
        "Parola sıfırlama isteği",
        $"""
        Merhaba {fullName},

        Hesabınız için parola sıfırlama isteği aldık. Yeni parolanızı belirlemek için aşağıdaki bağlantıyı açın:

        {link}

        Bu bağlantı {(int)lifetime.TotalMinutes} dakika geçerlidir ve yalnızca bir kez kullanılabilir. Bağlantıyı kimseyle paylaşmayın.

        Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; parolanız değişmeyecektir.

        Hastane Randevu Sistemi
        """);

    public static EmailContent PasswordChanged(string fullName) => new(
        "password-changed",
        "Parolanız değiştirildi",
        $"""
        Merhaba {fullName},

        Hesabınızın parolası az önce sıfırlama bağlantısıyla değiştirildi. Açık tüm oturumlarınız kapatıldı; yeni parolanızla yeniden giriş yapabilirsiniz.

        Bu değişikliği siz yapmadıysanız lütfen hemen sistem yöneticinizle iletişime geçin.

        Hastane Randevu Sistemi
        """);

    /// <summary>Belirteç kimliği + amaç: aynı belirteç için aynı türde ikinci e-posta eklenmez.</summary>
    public static EmailOutboxMessage ToMessage(Guid tokenId, string kind, string toAddress, string toName, EmailContent content, DateTimeOffset now) =>
        EmailOutboxMessage.Create($"{tokenId:N}:{kind}", toAddress, toName, content.Subject, content.Body, content.TemplateKey, now);
}
