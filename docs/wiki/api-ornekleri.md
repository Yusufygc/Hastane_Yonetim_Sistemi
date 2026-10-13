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

## Notlar / tuzaklar
- **Türkçe karakter (ş, ı, ğ…) ve Windows:** `--data '{...}'` ile komut satırı argümanı olarak gönderilen gövde Windows'ta ANSI kod sayfasına çevrilir; sunucu geçersiz UTF-8'i `400 Geçersiz istek` ile reddeder. Çözüm: gövdeyi UTF-8 dosyadan (`--data @hasta.json`) veya stdin'den (`--data-binary @-`) gönderin, ya da Scalar UI kullanın.
- **Hız sınırı:** `register`/`login`/`refresh` IP başına dakikada 10 istek; aşılırsa `429` gelir, bir dakika bekleyin.
- Bilinmeyen rota anonim kullanıcıya `404` değil `401` döner (varsayılan kapalı politika).
- Eski cookie/CSRF akışı kaldırıldı (`GET /api/auth/csrf` yok).

## Otomatik smoke test: `scripts/smoke-test.sh`
Çalışan API'yi uçtan uca dener (kayıt, giriş, `me`, katalog, 401/403, refresh + rotation, çıkış, süper yönetici akışı); her çalıştırmada benzersiz e-posta ve geçerli T.C. numarası üretir.
```bash
./scripts/smoke-test.sh                            # https://localhost:7032
BASE=http://localhost:5284 ./scripts/smoke-test.sh
```
- Süper yönetici bilgilerini `appsettings.Development.json`'dan okur (veya `ADMIN_EMAIL`/`ADMIN_PASSWORD` ortam değişkenleri).
- Yaklaşık 9 `auth` isteği yapar; dakikada 10 sınırı yüzünden aralıksız iki kez çalıştırmayın.
- Geliştirme veritabanına her çalıştırmada bir test hastası ekler.
