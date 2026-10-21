# API Örnekleri (curl, Scalar) ve Smoke Test

Çalışan API'yi elle denemek için örnekler. Uç listesi: [[api-uc-noktalari]]. Kimlik kuralları: [[kimlik-ve-yetki]]. Ortam: [[docker-ortam]].

## Önkoşul
```bash
docker compose up -d                               # PostgreSQL + Mailpit
dotnet run --project src/Presentation/Hospital.WebAPI --launch-profile https
```
Adresler (`launchSettings.json`): `https://localhost:7032` ve `http://localhost:5284`. Geliştirme sertifikası güvenilir değilse curl'e `-k` ekleyin. İlk açılışta (Development) migration + seed çalışır: varsayılan şube, örnek hizmetler ve süper yönetici (`appsettings.Development.json` → `Seed:SuperAdmin`).

## Hasta kaydı — tek adım
Kimlik doğrulama JWT Bearer olduğu için kayıt/giriş çağrıları ek başlık veya ön adım istemez:

```bash
curl -k https://localhost:7032/api/auth/register \
  --request POST \
  --header 'Content-Type: application/json' \
  --data @hasta.json
# -> 201 {"userId":"..."}
```

`hasta.json` (Türkçe karakter içeren gövdeyi **dosyadan** gönderin, bkz. aşağıdaki not):
```json
{
  "email": "ayse.yilmaz@example.com",
  "password": "<en az 8 karakter, en az bir harf ve bir rakam>",
  "firstName": "Ayşe",
  "lastName": "Yılmaz",
  "identityNumber": "10000000146",
  "birthDate": "1990-05-17",
  "phone": null
}
```
- `identityNumber`: sağlama basamakları geçerli 11 haneli T.C. numarası olmalı (`10000000146` yalnızca test için bilinen geçerli bir örnektir). Aynı numara/e-posta ikinci kez kaydedilemez (`409`).
- `birthDate`: `yyyy-MM-dd`; gelecekte olamaz.
- Yanıtlar: `201` başarı · `400` doğrulama (alan bazlı `errors`) · `409` çakışma · `429` hız sınırı.

## Giriş, token kullanımı, yenileme, çıkış
```bash
# Giriş -> accessToken (15 dk) + refreshToken (7 gün)
curl -k https://localhost:7032/api/auth/login --request POST \
  --header 'Content-Type: application/json' \
  --data '{"email":"ayse.yilmaz@example.com","password":"<PAROLA>"}'
# -> {"accessToken":"eyJ...","accessTokenExpiresAt":"...","refreshToken":"...","refreshTokenExpiresAt":"...","user":{...}}

# Korumalı uç: token Authorization başlığında
curl -k https://localhost:7032/api/auth/me --header "Authorization: Bearer <ACCESS_TOKEN>"

# Erişim token'ı dolunca yenile (eski refreshToken iptal olur, yenisi gelir)
curl -k https://localhost:7032/api/auth/refresh --request POST \
  --header 'Content-Type: application/json' --data '{"refreshToken":"<REFRESH_TOKEN>"}'

# Çıkış (refreshToken'ı iptal eder)
curl -k https://localhost:7032/api/auth/logout --request POST \
  --header "Authorization: Bearer <ACCESS_TOKEN>" \
  --header 'Content-Type: application/json' --data '{"refreshToken":"<REFRESH_TOKEN>"}'
```
Herkese açık katalog giriş gerektirmez: `GET /api/catalog/hospitals`, `/hospitals/{id}/departments`, `/hospitals/{id}/services`.

## Scalar ile deneme (`/scalar/v1`)
1. Sol menüde etiketler: **Auth, Catalog, Organization, Hospitals, Departments, Services**.
2. **Catalog → GET /api/catalog/hospitals** → *Send*: token gerekmez, `200` ve seed verisi (Merkez Şube, çalışma saatleri) görünür.
3. **Auth → POST /api/auth/register** gövdesini doldurup gönderin (`201`). Ardından **POST /api/auth/login** → yanıttaki `accessToken` değerini kopyalayın.
4. Sağ üstte/Authentication bölümünde **Bearer** alanına yapıştırın (başına `Bearer ` yazmayın). Artık korumalı uçlar çalışır: **GET /api/auth/me** → `200`, rol `PATIENT`.
5. Yetki denemeleri: hasta token'ı ile **GET /api/hospitals** → `403`; süper yönetici (seed) token'ı ile → `200`.
6. Her yetkili uçta açıklamada **Yetki: `<Politika>`** notu görünür; kilit simgesi/Authorize gereksinimi o ucun Bearer istediğini gösterir.
7. Bakılacak yanıt kodları: `201` oluşturma · `204` güncelleme/pasifleştirme · `400` doğrulama (`errors`) · `401` token yok/süresi dolmuş · `403` rol yetersiz · `404` yok **veya kapsam dışı** · `409` çakışma/aynı isim · `429` hız sınırı.

## Hata aldığımda nereye bakmalıyım?
Her hata yanıtı JSON'dur ve nedenini söyler (ayrıntı: [[hata-yonetimi]]):
- `code` → hatanın türü (`request.invalid_body`, `validation.failed`, `auth.unauthorized`, `*.name_taken` …).
- `errors` → **hangi alanın** neden hatalı olduğu (alan adları JSON'daki gibi camelCase).
- `detail` → okunabilir açıklama (ör. token süresi dolmuş → `/api/auth/refresh`).
- `traceId` → 500 gibi sunucu hatalarında loglarla eşleştirmek için.

Sık hatalar: `birthDate` **`yyyy-MM-dd`** olmalı (`1999-11-11`, `11.11.1999` değil) · `phone` **metin** olmalı (`"5444444444"`, tırnaklı) · `identityNumber` geçerli 11 haneli T.C. · parola ≥ 8 karakter, harf+rakam · `Content-Type: application/json`.

## Faz 4 örnekleri (slot, randevu)
```bash
# 1) Anonim: müsait slotlar (tarih yalnız yyyy-MM-dd; yanıtta UTC startAt/endAt + şubenin yerel saati localStart)
curl -sk "https://localhost:7032/api/catalog/doctors/{doctorId}/slots?serviceId={serviceId}&date=2026-11-02"
# 2) Hasta: slotlardan birinin startAt değeriyle randevu talep et (201 {id}); bitişi sunucu hesaplar
curl -sk -X POST https://localhost:7032/api/appointments -H "Authorization: Bearer $PATIENT" -H "Content-Type: application/json" \
  -d '{"doctorId":"...","serviceId":"...","startAt":"2026-11-02T06:00:00Z","patientNote":"Baş ağrısı"}'
# 3) Doktor: onayla (204) veya nedenle reddet; tamamlama randevu başladıktan sonra
curl -sk -X POST https://localhost:7032/api/appointments/{id}/approve -H "Authorization: Bearer $DOCTOR"
curl -sk -X POST https://localhost:7032/api/appointments/{id}/reject  -H "Authorization: Bearer $DOCTOR" -H "Content-Type: application/json" -d '{"reason":"O gün izinliyim"}'
# 4) Hasta/doktor/yönetici: iptal (neden zorunlu); hasta onaylı randevuyu başlamasına 2 saatten az kala iptal edemez
curl -sk -X POST https://localhost:7032/api/appointments/{id}/cancel -H "Authorization: Bearer $PATIENT" -H "Content-Type: application/json" -d '{"reason":"Plan değişti"}'
# 5) Listeler
curl -sk "https://localhost:7032/api/patients/me/appointments?status=APPROVED" -H "Authorization: Bearer $PATIENT"
curl -sk "https://localhost:7032/api/doctors/me/schedule" -H "Authorization: Bearer $DOCTOR"
```
Scalar'da: önce katalogdan doktor/hizmet kimliğini al, slot ucundan `startAt` kopyala, hastayla **POST /api/appointments** → `201`; aynı `startAt` ile ikinci hasta → `409 appointment.slot_unavailable`; doktor token'ıyla **approve** → `204`; hasta token'ıyla **approve** → `403`. Sık hatalar: `date` biçimi (`02.11.2026` değil `2026-11-02`) · `startAt` slot başlangıcıyla birebir aynı olmalı (09:07 gibi ızgara dışı saat 409) · hizmet doktorda **onaylı** değilse 400 `appointment.service_not_offered` · red/iptalde `reason` boş olamaz.

## Faz 3 örnekleri (doktor, hizmet talebi, uygunluk)
Yönetici (süper) token'ı ile doktor aç, doktor token'ı ile hizmet talep et, yönetici onaylasın:
```bash
# 1) Yönetici: doktor oluştur (departmentId = bir bölüm kimliği; parolayı yönetici verir)
curl -sk -X POST https://localhost:7032/api/doctors -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  --data-binary @doktor.json   # {"departmentId":"...","email":"d@x.test","password":"Doktor1234x","firstName":"Mehmet","lastName":"Demir","specialty":"Dahiliye","diplomaNo":"D-100"}
# 2) Doktor: giriş yap, hizmet talep et (202), uygunluk ekle (şube saatleri içinde)
curl -sk -X POST https://localhost:7032/api/doctors/me/services -H "Authorization: Bearer $DOCTOR" -H "Content-Type: application/json" -d '{"serviceId":"..."}'
curl -sk -X POST https://localhost:7032/api/doctors/me/availability -H "Authorization: Bearer $DOCTOR" -H "Content-Type: application/json" \
  -d '{"day":1,"startTime":"09:00","endTime":"12:00","validFrom":"2026-10-21"}'     # day: 0=Pazar … 6=Cumartesi
# 3) Yönetici: bekleyen talepleri gör ve onayla (204)
curl -sk https://localhost:7032/api/doctor-service-requests?status=pending -H "Authorization: Bearer $ADMIN"
curl -sk -X POST https://localhost:7032/api/doctors/{doctorId}/services/{serviceId}/approve -H "Authorization: Bearer $ADMIN"
# 4) Anonim: onaylı doktorlar (kişisel veri yok)
curl -sk "https://localhost:7032/api/catalog/doctors?hospitalId=...&serviceId=..."
```
Geliştirmede seed bir örnek doktor açar (`Seed:SampleDoctor`: `doktor@hastane.local` / `Doktor12345x`, Dahiliye, **Genel Muayene onaylı**, hafta içi 09:00-12:00 uygun). Sık hatalar: şube kapalı günü veya saatleri dışı uygunluk → `400 doctor.availability_outside_hours`; kesişen pencere → `409 doctor.availability_overlap`; `validFrom` biçimi `yyyy-MM-dd`; `startTime`/`endTime` `HH:mm`.

## Notlar / tuzaklar
- **Türkçe karakter (ş, ı, ğ…) ve Windows:** `--data '{...}'` ile komut satırı argümanı olarak gönderilen gövde Windows'ta ANSI kod sayfasına çevrilir; sunucu geçersiz UTF-8'i `400 Geçersiz istek` ile reddeder. Çözüm: gövdeyi UTF-8 dosyadan (`--data @hasta.json`) veya stdin'den (`--data-binary @-`) gönderin, ya da Scalar UI kullanın.
- **Hız sınırı:** `register`/`login`/`refresh`/`change-password` IP başına dakikada 10 istek (**Development'ta 100**, `appsettings.Development.json`); aşılırsa `429` gelir, bir dakika bekleyin.
- Bilinmeyen rota anonim kullanıcıya `404` değil `401` döner (varsayılan kapalı politika).
- Eski cookie/CSRF akışı kaldırıldı (`GET /api/auth/csrf` yok).

## Otomatik smoke test: `scripts/smoke-test.sh`
Çalışan API'yi uçtan uca dener (kayıt, giriş, `me`, katalog, 401/403, refresh + rotation, çıkış, süper yönetici akışı); her çalıştırmada benzersiz e-posta ve geçerli T.C. numarası üretir.
```bash
./scripts/smoke-test.sh                            # https://localhost:7032
BASE=http://localhost:5284 ./scripts/smoke-test.sh
```
- Süper yönetici bilgilerini `appsettings.Development.json`'dan okur (veya `ADMIN_EMAIL`/`ADMIN_PASSWORD` ortam değişkenleri).
- Yaklaşık 12 `auth` isteği yapar; Development ayarında sınır 100/dk olduğundan sorun çıkmaz, Production ayarlarında dakikada 10 sınırı yüzünden aralıksız iki kez çalıştırmayın.
- Bölüm 7 (Faz 4): anonim slot sorgusu (geçersiz tarih biçimi dahil), hasta randevu talebi → çift talep 409 → doktor onayı → hasta/doktor yetki sınırları → iptal (tekrar iptal 409) → hasta/doktor/yönetici listeleri. Randevuyu iptal ettiğinden slotu serbest bırakır, betik tekrar çalıştırılabilir.
- Bölüm 6 (Faz 3): hasta profili (maskeli T.C.), rol sınırları, anonim katalog doktorları, örnek doktor (`DOCTOR_EMAIL`/`DOCTOR_PASSWORD` ile değişir) ve yönetici listeleri.
- Geliştirme veritabanına her çalıştırmada bir test hastası ekler.
