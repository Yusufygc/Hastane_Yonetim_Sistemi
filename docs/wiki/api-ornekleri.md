# API Örnekleri (curl) ve Smoke Test

Çalışan API'yi elle denemek için örnekler. Uç listesi: [[api-uc-noktalari]]. Kimlik/CSRF kuralları: [[kimlik-ve-yetki]]. Ortam: [[docker-ortam]].

## Önkoşul
```bash
docker compose up -d                               # PostgreSQL + Mailpit
dotnet run --project src/Presentation/Hospital.WebAPI --launch-profile https
```
Adresler (`launchSettings.json`): `https://localhost:7032` ve `http://localhost:5284`. Geliştirme sertifikası güvenilir değilse curl'e `-k` ekleyin. İlk açılışta (Development) migration + seed çalışır: varsayılan şube, örnek hizmetler ve süper yönetici (`appsettings.Development.json` → `Seed:SuperAdmin`).

## Hasta kaydı: neden iki adım?
`/api` altındaki **tüm yazma istekleri** (POST/PUT/DELETE) geçerli bir CSRF belirteci ister ([[kararlar]] K-26). Doğrudan `POST /api/auth/register` çağırmak `400 csrf.invalid` döner. Önce belirteç ve cookie alınır, sonra istek belirteç başlığıyla gönderilir:

```bash
# 1) Belirteç + antiforgery cookie'si (cookies.txt dosyasına yazılır)
curl -k -c cookies.txt https://localhost:7032/api/auth/csrf
# -> {"headerName":"X-XSRF-TOKEN","token":"<TOKEN>"}

# 2) Kayıt (token'ı yukarıdaki çıktıdan alın)
curl -k -b cookies.txt -c cookies.txt https://localhost:7032/api/auth/register \
  --request POST \
  --header 'Content-Type: application/json' \
  --header 'X-XSRF-TOKEN: <TOKEN>' \
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
- Yanıtlar: `201` başarı · `400` doğrulama (alan bazlı `errors`) veya eksik CSRF · `409` çakışma · `429` hız sınırı.

## Giriş ve oturum
Kimlik değişince (giriş/çıkış) CSRF belirteci de değişir: **her yazma isteğinden önce yeni belirteç alın.**
```bash
curl -k -b cookies.txt -c cookies.txt https://localhost:7032/api/auth/csrf          # yeni <TOKEN>
curl -k -b cookies.txt -c cookies.txt https://localhost:7032/api/auth/login \
  --request POST --header 'Content-Type: application/json' --header 'X-XSRF-TOKEN: <TOKEN>' \
  --data '{"email":"ayse.yilmaz@example.com","password":"<PAROLA>"}'                 # 200, cookie set edilir
curl -k -b cookies.txt https://localhost:7032/api/auth/me                              # 200 {"role":"PATIENT",...}
```
Herkese açık katalog giriş gerektirmez: `GET /api/catalog/hospitals`, `/hospitals/{id}/departments`, `/hospitals/{id}/services`.

## Notlar / tuzaklar
- **Türkçe karakter (ş, ı, ğ…) ve Windows:** `--data '{...}'` ile komut satırı argümanı olarak gönderilen gövde Windows'ta ANSI kod sayfasına çevrilir; sunucu geçersiz UTF-8'i `400 Geçersiz istek` ile reddeder. Çözüm: gövdeyi UTF-8 dosyadan (`--data @hasta.json`) veya stdin'den (`--data-binary @-`) gönderin, ya da Scalar UI (`/scalar/v1`) kullanın.
- **Hız sınırı:** `login`/`register` IP başına dakikada 10 istek; aşılırsa `429` gelir, bir dakika bekleyin.
- Hatalı giriş, kilitli/pasif hesap ve bilinmeyen e-posta aynı `401 auth.invalid_credentials` yanıtını verir.

## Otomatik smoke test: `scripts/smoke-test.sh`
Çalışan API'yi uçtan uca dener (CSRF, kayıt, giriş, `me`, katalog, yetki 403/401, çıkış, süper yönetici akışı); her çalıştırmada benzersiz e-posta ve geçerli T.C. numarası üretir.
```bash
./scripts/smoke-test.sh                            # https://localhost:7032
BASE=http://localhost:5284 ./scripts/smoke-test.sh
```
- Süper yönetici bilgilerini `appsettings.Development.json`'dan okur (veya `ADMIN_EMAIL`/`ADMIN_PASSWORD` ortam değişkenleri).
- Dakikada 10 istek sınırı yüzünden aralıksız iki kez çalıştırmayın (ikincisi `429` alabilir).
- Geliştirme veritabanına her çalıştırmada bir test hastası ekler.
- 2026-10-13 sonucu: **17/17 başarılı** (Faz 2 sistemi çalışıyor).
