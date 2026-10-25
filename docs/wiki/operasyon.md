# Operasyon ve Dağıtım

Faz 6 (2026-10-25). Ortam: [[docker-ortam]]. Güvenlik: [[kimlik-ve-yetki]]. Bildirimler: [[bildirimler]]. Karar kaydı: [[kararlar]] K-62…K-70.

## Çalıştırma seçenekleri
| Amaç | Komut |
|---|---|
| Geliştirme (yalnız altyapı container'da) | `docker compose up -d` → `dotnet run --project src/Presentation/Hospital.WebAPI` |
| Her şey container'da (PostgreSQL + Mailpit + API) | `cp .env.example .env` → `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` → API `http://localhost:8080` |
| Testler | `./scripts/test-docker.ps1` (Linux container; PostgreSQL ve Mailpit gerekir) |
| Canlı smoke test | `BASE=http://localhost:8080 ./scripts/smoke-test.sh` |

API imajı çok aşamalıdır (`Dockerfile`): SDK 9 ile derlenir, `aspnet:9.0` üzerinde **root olmayan** kullanıcıyla 8080'de çalışır; gizli ayar imaja gömülmez. `docker-compose.app.yml` zorunlu gizli değişkenler eksikse açık hata verir; sağlık denetimi `/health` (veritabanı dahil).

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
6. TLS sonlandırma ters vekilde (API 8080 düz HTTP dinler); hız sınırı IP'ye göre olduğundan vekilin istemci IP'sini ilettiğinden emin olun.
7. `/health` izleme; log toplama `CorrelationId` alanını indeksler.

## Bilinçli sınırlar
- Kullanıcı bazlı/sürekli hız sınırı yok (yalnız kimlik uçları, IP başına).
- Anahtar döndürme, e-posta/telefon alan şifrelemesi, denetim izi (audit log), outbox izleme ucu yok.
- Sayfalama sayfa numarası tabanlıdır (çok büyük ofsetlerde imleç tabanlı sayfalamaya göre yavaşlar); veri hacmi ödev ölçeğindedir.
