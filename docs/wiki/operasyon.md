# Operasyon ve Dağıtım

Faz 6 (2026-10-25). Ortam: [[docker-ortam]]. Güvenlik: [[kimlik-ve-yetki]]. Bildirimler: [[bildirimler]]. Karar kaydı: [[kararlar]] K-62…K-70.

## Çalıştırma seçenekleri
| Amaç | Komut |
|---|---|
| Geliştirme (yalnız altyapı container'da) | `docker compose up -d` → `dotnet run --project src/Presentation/Hospital.WebAPI` |
| Her şey container'da (PostgreSQL + Mailpit + API + **arayüz**) | `cp .env.example .env` → `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` → arayüz `http://localhost:8081`, API `http://localhost:8080` |
| Testler | `./scripts/test-docker.ps1` (Linux container; PostgreSQL ve Mailpit gerekir) |
| Canlı smoke test | `BASE=http://localhost:8080 ./scripts/smoke-test.sh` |

API imajı çok aşamalıdır (`Dockerfile`): SDK 9 ile derlenir, `aspnet:9.0` üzerinde **root olmayan** kullanıcıyla 8080'de çalışır; gizli ayar imaja gömülmez. `docker-compose.app.yml` zorunlu gizli değişkenler eksikse açık hata verir; sağlık denetimi `/health` (veritabanı dahil).

## Web imajı (arayüz) ve ters vekil
`web/Dockerfile` çok aşamalıdır: `node:22-alpine` ile `npm ci && npm run build`, ardından `nginxinc/nginx-unprivileged` (root olmayan kullanıcı, 8080) ile sunulur. `docker-compose.app.yml`'deki `web` servisi API sağlıklı olunca açılır (`depends_on: service_healthy`), host portu `WEB_PORT` (varsayılan 8081). Gizli bilgi imaja girmez (arayüz herkese açık statik dosyadır) — [[kararlar]] K-82.

nginx (`web/nginx/default.conf.template`, açılışta `API_UPSTREAM` ile doldurulur):
- **Tek origin:** `/api/` → API'ye vekil; tarayıcı için arayüz ve API aynı origin olduğundan CORS gerekmez (`VITE_API_URL` verilmez).
- **SPA yönlendirmesi:** bilinmeyen yol `index.html`'e düşer (React Router).
- **Önbellek:** `/assets/` (içerik karmalı dosya adı) bir yıl, `immutable`; `index.html` **no-cache** (yeni sürüm hemen görünür). gzip açık.
- **Güvenlik başlıkları** (`security-headers.conf`, her location'a include edilir; nginx'te location içinde `add_header` üst düzeyi devralmaz): `Content-Security-Policy` (betikler yalnız aynı origin; `style-src 'unsafe-inline'` three.js tuvali için), `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`. `server_tokens off`; nokta dosyaları (`.env`, `.git`) sunulmaz.
- Sağlık: `GET /healthz` (imaj `HEALTHCHECK`'i kullanır).

### Vekil arkasında gerçek istemci IP'si (X-Forwarded-For)
Hız sınırı IP'ye göre bölümlenir; vekil arkasında herkes vekilin IP'siyle görünür ve **tüm kullanıcılar tek kovayı paylaşır** (kimlik uçlarında dakikada 10 istek → bir kullanıcı herkesi kilitler). Bu yüzden API `X-Forwarded-For/Proto` başlığını okuyabilir — **varsayılan kapalı**:

| Anahtar | Anlam |
|---|---|
| `ForwardedHeaders:Enabled` | `true` ise başlık dikkate alınır (varsayılan `false`) |
| `ForwardedHeaders:TrustedNetworks` | Güvenilen vekil ağları (CIDR listesi, örn. `172.16.0.0/12`) |
| `ForwardedHeaders:TrustedProxies` | Güvenilen vekil IP'leri |

Kurallar: başlık **yalnız güvenilen kaynaktan** gelirse okunur (aksi halde istemci başlığı taklit edip sınırı atlatırdı); `Enabled=true` iken güvenilen ağ/IP verilmezse veya CIDR bozuksa uygulama **açılışta** açık hata verir ("herkese güven" yok); tek vekil atlaması (`ForwardLimit=1`). nginx istemci IP'sini `$remote_addr` ile **sıfırdan yazar** (gelen başlığı eklemez). Compose'ta `172.16.0.0/12` (Docker bridge) güvenilir; API portu hosta da açık olduğundan bu **yerel/demo** ayarıdır — üretimde API yalnız iç ağda olmalı ve vekilin gerçek adresi yazılmalıdır. Testler: `ForwardedHeadersTests`.

## Yapılandırma anahtarları
Ortam değişkeninde `:` yerine `__` kullanılır (ör. `Jwt__SigningKey`).

| Anahtar | Varsayılan | Not |
|---|---|---|
| `ConnectionStrings:Default` | — | PostgreSQL bağlantısı (zorunlu) |
| `Jwt:SigningKey` | — | **Zorunlu**, ≥ 32 bayt; yoksa uygulama başlamaz. `Issuer`/`Audience`/`AccessTokenMinutes` (15)/`RefreshTokenDays` (7) |
| `Security:DataProtection:Key` | — | **Zorunlu**, Base64, tam 32 bayt (`openssl rand -base64 32`); T.C. kimlik no şifreleme anahtarı. Yoksa/hatalıysa uygulama başlamaz |
| `Database:MigrateOnStartup` / `SeedOnStartup` | `false` | Migration + T.C. şifreleme yükseltmesi / ilk kurum ve süper yönetici |
| `Seed:SuperAdmin:Email` / `Password` | — | İlk süper yönetici (üretimde ortam değişkeni) |
| `Seed:SampleData`, `Seed:SampleDoctor:*` | `false` | Demo verisi; üretimde kapalı |
| `Email:Smtp:*`, `Email:Outbox:*` | bkz. [[bildirimler]] | SMTP ve outbox işleyici |
| `Appointment:SlotGridMinutes` / `MinLeadMinutes` / `MaxBookingHorizonDays` / `CancellationDeadlineHours` | 15 / 60 / 60 / 2 | [[randevu-kurallari]] |
| `Appointment:PendingExpiry:Enabled` / `Hours` / `PollSeconds` / `BatchSize` | `true` / 48 / 300 / 50 | Doktor yanıt vermeyen bekleyen talebi otomatik reddeder |
| `RateLimiting:Auth:PermitLimit` / `WindowSeconds` | 10 / 60 | `register`/`login`/`refresh`/`change-password` IP başına |
| `Cors:AllowedOrigins` | boş | Arayüz origin'leri (joker yok) |
| `OpenApi:Enabled` | `false` | Development dışında OpenAPI belgesi + Scalar'ı açar |

## Gizli bilgiler ve anahtarlar
- JWT anahtarı, veri şifreleme anahtarı, SMTP parolası, süper yönetici parolası: **yalnız ortam değişkeni/gizli depo**; depoda yalnız geliştirme değerleri (`appsettings.Development.json`, `.env.example`).
- **Veri şifreleme anahtarını yedekleyin.** Kaybolursa şifreli T.C. numaraları çözülemez; değişirse mevcut şifreli veri okunamaz (uygulama 500 verir, sessizce yanlış veri dönmez). Anahtar döndürme (rotation) bilinçli olarak yoktur: şifreli değer `enc:v1:` önekini taşır, ileride `v2` + toplu yeniden şifreleme eklenebilir.
- `Hardening` migration'ı geri alınmamalıdır (şifreli değer eski `char(11)` sütununa sığmaz).

## İzleme ve loglar
- Her isteğe `X-Correlation-Id` atanır/korunur, yanıtta döner, hata yanıtının `traceId` alanıyla aynıdır ve log kapsamındadır: şikâyet → tek kimlikle log araması.
- İstek özeti: `HTTP {Method} {Route} -> {Status} ({ms} ms)`; **rota şablonu** loglanır (gerçek yol/kimlik, sorgu, gövde, başlık loglanmaz); `/health` Debug seviyesindedir.
- EF komut logları (`Microsoft.EntityFrameworkCore.Database.Command`) varsayılan **Warning**: arka plan işleyicilerin her yoklama SQL'i logu doldurmaz; ayrıntı gerekirse bu kategoriyi `Information` yapın.
- Arka plan işleri yalnız kimlik ve sonuç loglar (alıcı adresi/e-posta gövdesi yok).
- Sağlık: `GET /health` (veritabanı denetimi dahil), anonim.

## Arka plan işleri
| İş | Ne yapar | Kapatma |
|---|---|---|
| `EmailOutboxProcessor` | Bekleyen e-postaları SMTP ile gönderir, yeniden dener ([[bildirimler]]) | `Email:Outbox:Enabled=false` |
| `PendingAppointmentExpiryProcessor` | 48 saat içinde karara bağlanmayan (veya başlangıcı geçmiş) bekleyen talebi reddeder; neden "Doktor süresi içinde yanıt vermediği için…"; slot serbest kalır, hastaya e-posta gider | `Appointment:PendingExpiry:Enabled=false` |

İkisi de her öğeyi ayrı kapsam/transaction'da işler; hata işleyiciyi durdurmaz; çok örnekte çift işlem yapmaz (outbox `SKIP LOCKED`; zaman aşımı `xmin` eşzamanlılık belirteci).

## Veri yükseltmeleri
- **T.C. kimlik şifreleme (KVKK):** `Database:MigrateOnStartup` açıkken migration'dan hemen sonra `IdentityNumberUpgrader` kör indeksi boş (eski düz metin) hastaları şifreler; idempotenttir. Migration otomatik değilse dağıtım adımı olarak aynı sınıf çalıştırılmalıdır. Yükseltme tamamlanana dek eski kayıtlar okunabilir (düz metin geçişi) ama yinelenen TC denetimine girmez.

## Performans bakışı (indeks gözden geçirmesi)
Kritik sorgular gerçek PostgreSQL planıyla sınanır (`QueryPlanTests`: `enable_seqscan=off` ile "Seq Scan" çıkmaması = kullanılabilir indeks var).

| Sorgu | İndeks |
|---|---|
| Doktorun dolu slotları / çakışma | `ex_doctor_no_overlap` (gist), `ix_appointment_doctor_id_start_at` |
| Hasta geçmişi (yeniden eskiye) | `ix_appointment_patient_id_start_at` (DESC) |
| Yönetici listesi (şube + durum) | `ix_appointment_hospital_id_status_start_at` |
| Bekleyen talep zaman aşımı taraması | `ix_appointment_pending_created_at` (**kısmi**, `WHERE status='PENDING'`) |
| Yönetici onay kuyruğu | `ix_doctor_service_pending` (**kısmi**) — önceki genel `(status, created_at)` indeksinin yerine |
| Outbox bekleyenler / tekrar önleme | `ix_email_outbox_pending` (**kısmi**), `ix_email_outbox_dedup_key` (benzersiz) |
| TC kör indeks araması | `ix_patient_identity_number_hash` (benzersiz) |
| Giriş / yenileme | `ix_user_account_email`, `refresh_token.token_hash` benzersiz |

Diğer: liste uçları sayfalıdır (en çok 200); okuma sorguları `AsNoTracking` projeksiyondur; slot hesabı doktor başına tek bölünmüş sorguyla yüklenir (`GetForSchedulingAsync`).

## API sözleşmesi (donduruldu)
- OpenAPI belgesi `docs/api/openapi.v1.json` dosyasında **dondurulmuştur**. `OpenApiContractTests` üretilen belgeyi bu dosyayla karşılaştırır; sözleşme (yol, parametre, yanıt, şema, yetki) değişirse test kırılır.
- Bilinçli değişiklikten sonra dosyayı güncellemek için: `./scripts/update-openapi-snapshot.ps1` (Linux container'da üretir), dosya farkını inceleyip commit edin. Arayüz (Faz 7) bu dosyayı kaynak alır.
- Her uç Türkçe özet, gereken yetki ve olası hata yanıtlarını (ProblemDetails) belgeler; eksik özet testi kırar.

## Yetki matrisi
`EndpointPolicyMatrixTests`: tüm uçların politikası **açık bir tabloyla** doğrulanır (yeni uç tabloya eklenmeden testi geçemez; politikası gevşetilen uç kırar) ve her uç 6 kimlikle (anonim, hasta, doktor, personel, şube yöneticisi, süper yönetici) çağrılarak 401/403 davranışı politika tablosuna göre denetlenir. `IdorMatrixTests` kapsam/sahiplik dışı erişimin 404 olduğunu ve kaynağın değişmediğini doğrular.

## Dağıtım kontrol listesi
1. `Jwt__SigningKey`, `Security__DataProtection__Key` (yedekli), `ConnectionStrings__Default`, `Seed__SuperAdmin__*` ortam değişkenleri verildi.
2. `Email__Smtp__Host/Port/UserName/Password`, `Email__Smtp__Security=StartTls|Ssl`, `FromAddress`.
3. `Cors__AllowedOrigins__0…` arayüz origin'i; `OpenApi__Enabled` yalnız gerekiyorsa.
4. `Seed__SampleData=false`; örnek doktor/hesap yok.
5. `Database__MigrateOnStartup=true` (veya migration + `IdentityNumberUpgrader` ayrı adım).
6. TLS sonlandırma ters vekilde (API 8080 düz HTTP dinler); hız sınırı IP'ye göre olduğundan vekil `X-Forwarded-For` iletmeli ve API'de `ForwardedHeaders:Enabled=true` + `TrustedNetworks/TrustedProxies` (vekilin gerçek adresi) verilmelidir. Arayüz için `web/` imajı (nginx) kullanılabilir; TLS yine bunun önündeki vekilde sonlanır.
7. `/health` izleme; log toplama `CorrelationId` alanını indeksler.

## Bilinçli sınırlar
- Kullanıcı bazlı/sürekli hız sınırı yok (yalnız kimlik uçları, IP başına).
- Anahtar döndürme, e-posta/telefon alan şifrelemesi, denetim izi (audit log), outbox izleme ucu yok.
- Sayfalama sayfa numarası tabanlıdır (çok büyük ofsetlerde imleç tabanlı sayfalamaya göre yavaşlar); veri hacmi ödev ölçeğindedir.
