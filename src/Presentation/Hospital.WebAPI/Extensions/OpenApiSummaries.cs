namespace Hospital.WebAPI.Extensions;

/// <summary>
/// Uç noktaların Türkçe özetleri (OpenAPI <c>summary</c>). Anahtar: <c>"METOT /yol"</c> (rota parametreleri <c>{ad}</c>, kısıtsız, sondaki
/// eğik çizgi yok). Yeni uç eklendiğinde buraya özet eklenmelidir; eksik özet sözleşme testini (<c>OpenApiContractTests</c>) kırar.
/// </summary>
internal static class OpenApiSummaries
{
    private static readonly Dictionary<string, string> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- Kimlik
        ["POST /api/auth/register"] = "Hasta hesabı oluşturur (herkese açık kayıt yalnız hasta açar).",
        ["POST /api/auth/login"] = "Giriş yapar; erişim belirteci (accessToken) ve yenileme belirteci (refreshToken) döner.",
        ["POST /api/auth/refresh"] = "Yenileme belirteciyle yeni belirteç çifti alır (eski belirteç iptal edilir).",
        ["POST /api/auth/logout"] = "Oturumu kapatır: yenileme belirtecini iptal eder.",
        ["GET /api/auth/me"] = "Oturum açmış kullanıcının kimliğini, e-postasını ve rolünü döner.",
        ["POST /api/auth/forgot-password"] = "Şifremi unuttum: e-postaya tek kullanımlık sıfırlama bağlantısı gönderir (hesap olsun olmasın aynı 202 yanıtı).",
        ["POST /api/auth/reset-password"] = "E-postadaki belirteçle yeni parola belirler; tüm oturumlar kapanır, belirteç bir daha kullanılamaz.",
        ["POST /api/auth/change-password"] = "Kendi parolasını değiştirir; tüm oturumlar kapanır, yeniden giriş gerekir.",

        // ---- Anonim katalog
        ["GET /api/catalog/hospitals"] = "Aktif şubeleri ve çalışma saatlerini listeler.",
        ["GET /api/catalog/hospitals/{id}/departments"] = "Şubenin aktif bölümlerini listeler.",
        ["GET /api/catalog/hospitals/{id}/services"] = "Şubenin aktif hizmetlerini (süre, ücret) listeler.",
        ["GET /api/catalog/doctors"] = "Şubenin randevu alınabilir doktorlarını listeler (en az bir onaylı hizmeti olanlar; hizmete göre süzülebilir).",
        ["GET /api/catalog/doctors/{id}/slots"] = "Doktorun bir hizmet için belirli günde müsait slotlarını döner.",

        // ---- Hasta
        ["GET /api/patients/me"] = "Hasta kendi profilini görür (T.C. kimlik no maskeli).",
        ["PUT /api/patients/me"] = "Hasta kendi profilini günceller (ad, soyad, telefon, kan grubu).",
        ["GET /api/patients/me/appointments"] = "Hasta kendi randevularını listeler (sayfalı, yeniden eskiye).",

        // ---- Randevu
        ["POST /api/appointments"] = "Hasta randevu talep eder (bekleyen oluşur, slot rezerve edilir).",
        ["GET /api/appointments"] = "Yönetici kapsamındaki şubelerin randevularını listeler (sayfalı).",
        ["GET /api/appointments/{id}"] = "Randevu detayını döner (yalnız hasta, doktor veya kapsamdaki yönetici).",
        ["POST /api/appointments/{id}/approve"] = "Doktor bekleyen randevuyu onaylar.",
        ["POST /api/appointments/{id}/reject"] = "Doktor bekleyen randevuyu nedenle reddeder.",
        ["POST /api/appointments/{id}/complete"] = "Doktor başlamış onaylı randevuyu tamamlar.",
        ["POST /api/appointments/{id}/cancel"] = "Hasta, doktor veya yönetici randevuyu nedenle iptal eder.",

        // ---- Doktor (kendi)
        ["GET /api/doctors/me"] = "Doktor kendi profilini ve hizmet durumlarını görür.",
        ["PUT /api/doctors/me"] = "Doktor uzmanlığını ve telefonunu günceller.",
        ["GET /api/doctors/me/appointments"] = "Doktor kendi randevularını listeler (sayfalı, eskiden yeniye).",
        ["GET /api/doctors/me/schedule"] = "Doktor onaylı randevu takvimini görür (varsayılan şimdiden 7 gün).",
        ["GET /api/doctors/me/availability"] = "Doktor haftalık uygunluk pencerelerini listeler.",
        ["POST /api/doctors/me/availability"] = "Doktor uygunluk penceresi ekler (şube saatleri içinde, çakışmasız).",
        ["PUT /api/doctors/me/availability/{id}"] = "Doktor uygunluk penceresini günceller.",
        ["DELETE /api/doctors/me/availability/{id}"] = "Doktor uygunluk penceresini siler.",
        ["GET /api/doctors/me/time-off"] = "Doktor izin aralıklarını listeler.",
        ["POST /api/doctors/me/time-off"] = "Doktor izin ekler (aktif randevusu olan aralığa eklenemez).",
        ["DELETE /api/doctors/me/time-off/{id}"] = "Doktor iznini siler.",
        ["GET /api/doctors/me/services"] = "Doktor hizmet taleplerini ve onay durumlarını listeler.",
        ["POST /api/doctors/me/services"] = "Doktor kendi şubesindeki bir hizmeti sunmak için talep eder.",
        ["DELETE /api/doctors/me/services/{serviceId}"] = "Doktor bekleyen veya reddedilmiş hizmet talebini geri çeker.",

        // ---- Yönetici: doktor, personel, yönetici
        ["GET /api/doctors"] = "Yönetici kapsamındaki doktorları listeler (sayfalı).",
        ["POST /api/doctors"] = "Yönetici doktor hesabı oluşturur.",
        ["GET /api/doctors/{id}"] = "Yönetici doktor detayını ve hizmet durumlarını görür.",
        ["PUT /api/doctors/{id}"] = "Yönetici doktor bilgilerini günceller (bölüm aynı şubede kalmalı).",
        ["PUT /api/doctors/{id}/services"] = "Yönetici doktorun onaylı hizmet kümesini listeyle eşitler (doğrudan atama).",
        ["POST /api/doctors/{doctorId}/services/{serviceId}/approve"] = "Yönetici doktorun hizmet talebini onaylar.",
        ["POST /api/doctors/{doctorId}/services/{serviceId}/reject"] = "Yönetici doktorun bekleyen hizmet talebini reddeder.",
        ["DELETE /api/doctors/{doctorId}/services/{serviceId}"] = "Yönetici hizmeti doktordan kaldırır (gelecekte aktif randevusu varsa kaldırılamaz).",
        ["GET /api/doctor-service-requests"] = "Yönetici kapsamındaki doktor hizmet taleplerini durumuna göre listeler (sayfalı).",
        ["GET /api/staff"] = "Yönetici kapsamındaki personeli listeler (sayfalı).",
        ["POST /api/staff"] = "Yönetici personel hesabı oluşturur.",
        ["GET /api/admins"] = "Yöneticileri listeler (şube yöneticisi yalnız kendi şubesinin yöneticilerini görür).",
        ["POST /api/admins"] = "Süper yönetici yeni yönetici (süper veya şube yöneticisi) oluşturur.",
        ["POST /api/users/{id}/deactivate"] = "Yönetici hesabı pasifleştirir (oturumlar anında düşer; aktif randevusu varsa engellenir).",
        ["POST /api/users/{id}/activate"] = "Yönetici pasif hesabı yeniden etkinleştirir.",

        // ---- Yönetici: kurum
        ["GET /api/organization"] = "Süper yönetici kurum bilgisini görür.",
        ["PUT /api/organization"] = "Süper yönetici kurum adını günceller.",
        ["GET /api/hospitals"] = "Yönetici kapsamındaki şubeleri listeler.",
        ["POST /api/hospitals"] = "Süper yönetici yeni şube oluşturur.",
        ["GET /api/hospitals/{id}"] = "Yönetici şube detayını ve çalışma saatlerini görür.",
        ["PUT /api/hospitals/{id}"] = "Yönetici şube bilgilerini günceller.",
        ["PUT /api/hospitals/{id}/working-hours"] = "Yönetici şubenin haftalık çalışma saatlerini listeyle eşitler (listede olmayan gün kapalı).",
        ["POST /api/hospitals/{id}/deactivate"] = "Yönetici şubeyi pasifleştirir.",
        ["GET /api/hospitals/{hospitalId}/departments"] = "Yönetici şubenin tüm bölümlerini (pasifler dahil) listeler.",
        ["POST /api/hospitals/{hospitalId}/departments"] = "Yönetici şubeye bölüm ekler.",
        ["PUT /api/departments/{id}"] = "Yönetici bölümü yeniden adlandırır.",
        ["POST /api/departments/{id}/deactivate"] = "Yönetici bölümü pasifleştirir (aktif hizmeti varsa engellenir).",
        ["GET /api/services"] = "Yönetici kapsamındaki hizmetleri listeler (şubeye göre süzülebilir).",
        ["POST /api/services"] = "Yönetici hizmet ekler (sabit süre, ücret, para birimi).",
        ["GET /api/services/{id}"] = "Yönetici hizmet detayını görür.",
        ["PUT /api/services/{id}"] = "Yönetici hizmeti günceller (mevcut randevulardaki bilgi değişmez).",
        ["POST /api/services/{id}/deactivate"] = "Yönetici hizmeti pasifleştirir.",
    };

    public static string? Find(string method, string path) =>
        Entries.TryGetValue($"{method} {Normalize(path)}", out var summary) ? summary : null;

    /// <summary>Rota kısıtlarını (<c>{id:guid}</c> → <c>{id}</c>) ve sondaki eğik çizgiyi atar.</summary>
    private static string Normalize(string path)
    {
        var withoutConstraints = System.Text.RegularExpressions.Regex.Replace(path, @"\{(\w+)(:[^}]*)?\??\}", "{$1}");
        return withoutConstraints.Length > 1 ? withoutConstraints.TrimEnd('/') : withoutConstraints;
    }
}
