# Test Stratejisi

Kaynak: `plan.md` §11. Faz bağlantısı: [[faz-plani]].

| Katman | Araç | Kapsam |
|---|---|---|
| Unit | xUnit, AwesomeAssertions | State geçişleri, slot üretimi (pencere × çalışma saati × izin), snapshot, iptal süresi, doktor-hizmet onayı ([[randevu-kurallari]]) |
| Entegrasyon | Gerçek PostgreSQL (compose / CI service; geçici DB) | Exclusion constraint, EF eşlemeleri, outbox |
| Eşzamanlılık | Gerçek PostgreSQL | N paralel istek aynı slota → **tam 1 başarılı, N-1 `409`** |
| Mimari | NetArchTest | Katman/feature kuralları ([[mimari]]) |
| API / Yetki | `WebApplicationFactory` | Endpoint tarama, rol×uç matrisi, IDOR, JWT doğrulama (bozuk/sahte/süresi dolmuş token), refresh rotation ([[kimlik-ve-yetki]]) |

## Arayüz testleri (Faz 7)
`web/` altında Vitest + Testing Library (`npm test`, 50 test): http istemcisi, yardımcılar, uygulama akışları, doktor ve yönetim paneli akışları; CI `web` işi ayrıca lint, tip üretimi farkı ve derlemeyi denetler ([[arayuz]]).

## Arayüz yetkilendirme matrisi (Faz 7)
`web/src/RouteAccess.test.tsx` (10 test): anonim → giriş sayfasına yönlendirme (`/doktor`, `/yonetim`, `/randevularim`); hasta → `/doktor`, `/yonetim`; doktor → `/yonetim`, `/randevularim`; yönetici → `/doktor`, `/randevularim` engellenir ve ilgili korumalı uçlara **hiç istek atılmaz**; her rol kendi sayfasına girebilir. Canlı deneme sonuçları ve rol × uç tablosu: [[arayuz]] "Yetkilendirme denemesi".

## Vekil arkası (Faz 7)
`ForwardedHeadersTests`: güvenilen vekilden gelen istekte hız sınırı gerçek istemciye göre ayrılır; güvenilmeyen kaynaktan gelen `X-Forwarded-For` yok sayılır (taklitle sınır aşılamaz); varsayılan kapalı; hatalı yapılandırma (güvenilen ağ yok / bozuk CIDR) açılışta açık hata verir ([[operasyon]]).

## Faz 6 testleri
- **Yetki matrisi:** `EndpointMatrix` altın tablosu (67 uç → politika) + `EndpointPolicyMatrixTests` (6 kimlik × tüm uçlar). Yeni uç tabloya eklenmeden envanter testi kırılır ([[kararlar]] K-68).
- **IDOR:** `IdorMatrixTests` — başkasının randevu/profil/hizmet kaynağı 404.
- **Sözleşme:** `OpenApiContractTests` (veritabansız, `DbLessApiFixture`): üretilen OpenAPI `docs/api/openapi.v1.json` ile aynı mı; her uç özet/yetki/yanıt tipi; başarı kodu ve gövde şeması ([[operasyon]], K-67). Güncelleme: `scripts/update-openapi-snapshot.ps1`.
- **İzleme/sayfalama:** `CorrelationIdTests`, `PaginationTests` (başlıklar, sınırlar, 400).
- **Zaman aşımı:** `PendingExpiryTests`. **KVKK:** `AesGcmSensitiveDataProtectorTests` (birim) + `IdentityNumberEncryptionTests` (DB'de şifreli, kör indeks, yükseltme, anahtarsız başlamama).
- Bilinen sınır: EF modeli süreçte bir kez kurulup önbelleğe alındığından aynı süreçte farklı veri-anahtarlı iki uygulama kurulamaz; "yanlış anahtarla çözülemez" davranışı birim testindedir (`AesGcmSensitiveDataProtectorTests`).
- **Performans:** `QueryPlanTests` (`EXPLAIN`, `enable_seqscan=off` ile Seq Scan yok = kullanılabilir indeks var).

## Zorunlu çakışma senaryoları
Birebir aynı aralık; kısmi kesişim (baş/son/içinde/kapsayan); bitişik aralıklar (çakışmaz); farklı doktor aynı saat (çakışmaz); iptal/red sonrası slot yeniden alınabilir; aynı hasta iki doktorda aynı saat; DST geçiş günü; gün sonu (23:xx); uygunluk dışı / izin günü / hastane kapalı saat; `PENDING` doktor-hizmetiyle randevu talebi (reddedilmeli).

## Çalıştırma
- Hızlı döngü: `dotnet test Hospital.sln` (Windows'ta Smart App Control kendi DLL'inizi bazen engeller — bkz. [[docker-ortam]]).
- Güvenilir yol: `./scripts/test-docker.ps1` — derleme + testler Linux .NET 9 SDK container'ında; PostgreSQL için `host.docker.internal:5432`.
- **Faz 6 sonrası: 301 birim, 9 mimari, 350 entegrasyon (hepsi yeşil).** Faz 5 sonrası: **275 birim, 9 mimari, 279 entegrasyon** (birim: `EmailOutboxMessageTests` durum/geri çekilme/`FAILED`, `NotificationHandlerTests` alıcı seçimi + dedup + şablon; entegrasyon: `NotificationOutboxTests` olay→satır, **atomiklik (DB çakışmasıyla geri alınan randevunun e-postası da yok)**, 20 paralel talep → 1 doktor e-postası, dedup, gönderim/yeniden deneme/geri çekilme/vazgeçme, **4 işleyici paralel → çift gönderim yok**, `NotificationBackgroundTests` arka plan işleyici + SMTP kesinti/kurtarma, `MailpitDeliveryTests` **gerçek SMTP → Mailpit API doğrulaması**, `SmtpUnavailableTests` gerçek kapalı port). Önceki satır — Faz 4 sonrası: **251 birim, 9 mimari, 260 entegrasyon** (birim: `SlotCalculatorTests` ızgara/kesişim/izin/dolu/hazırlık/ufuk/geçerlilik + **DST** ileri geçiş ve 23/25 saatlik gün, `AppointmentStateTests` 20 geçiş×durum tablosu, `AppointmentTests` snapshot/olay/neden/süre sınırı, `Faz4ValidatorTests`; entegrasyon: `AppointmentSlotTests` anonim slot ucu, `AppointmentFlowTests` yaşam döngüsü + sahiplik/IDOR + kapsam + listeler + snapshot, `AppointmentConcurrencyTests` **HTTP'de 20 paralel talep → 1 × 201, 19 × 409**, çift onay/red ve çift iptal yarışları, `AppointmentGuardTests` gelecek-randevu denetimleri; `AppointmentOverlapConstraintTests` gerçek FK'li doktorla DB kısıtları). Önceki satır — Faz 3 sonrası: **162 birim, 9 mimari, 215 entegrasyon** (birim: `DoctorTests` talep/onay/atama + uygunluk çakışma/saat/dönem + izin, `StaffAndProfileTests`, `Faz3ValidatorTests`; entegrasyon: `DoctorManagementTests` hesap/kapsam/rol, `DoctorServiceFlowTests` talep→onay→katalog ve kapsam, `DoctorScheduleTests` uygunluk/izin + **DB exclusion constraint'lerinin ham SQL ile doğrudan sınanması (23P01)**, `AccountAndProfileTests` profil/parola/personel/yönetici/pasifleştirme). Önceki satır — hata yönetimi sonrası: 104 birim, 9 mimari, 156 entegrasyon (`RequestErrorTests`: gövde/parametre/JSON/içerik türü hataları, 401-405-415-429 gövdeleri, 422/500, Development ve Production ortamı; `RequestErrorTranslatorTests`: saf çevirici). Önceki satır — JWT geçişi sonrası: 104 birim, 9 mimari, 113 entegrasyon (JWT: imza/süre/issuer/audience/stamp reddi, refresh rotation + reuse detection, logout, CSRF yok). Faz 2 sonu (cookie dönemi): 81 birim, 9 mimari (DI kuralı dahil, [[di-kurallari]]), 91 entegrasyon testi (kayıt, giriş, kilitleme, CSRF, oturum iptali, rol 403, hız sınırı, TPT; çakışma kısıtları ve 20 paralel istek — [[randevu-kurallari]]; şube/bölüm/hizmet CRUD, çalışma saati eşitleme, **şube yöneticisi kapsam ihlalleri → 404**, anonim katalog, organizasyon).
- Test yardımcıları: `AdminSessions` (seed'li süper yönetici girişi, DB'ye şube yöneticisi yazma), `TestAdmin` (yalnız test değerleri), `ApiClient.PutAsync/DeleteAsync/SendAsync/LoginAsync`; Faz 3: `AdminSessions.CreateDoctorAsync/AddAvailabilityAsync/LoginAsync`, `TestDoctor` (seed örnek doktor + ortak parola); Faz 4: `AppointmentKit` (şube + doktor + onaylı hizmet + Pazartesi 09-12 uygunluk hazır senaryosu `BookingSetup`, slot/randevu yardımcıları, `MoveToPastAsync` ile başlamış randevu). Gerçek saat kullanılır (sahte saat JWT'yi bozardı); uzak tarih ve `Appointment:*` ayarlarının fabrika üzerinden geçersiz kılınmasıyla süre sınırı sınanır.

Test-only uçlar: `HospitalApiFactory(includeProbeEndpoints: true)` ile `/api/test/*` uçları (rol politikaları, hata üretimi) kaydedilir (randevu probe ucu Faz 4'te kaldırıldı, gerçek uç kullanılır); gerçek uygulamada bulunmaz, endpoint-yetki taramasına girmez.

## Ortam
Entegrasyon testleri `docker compose up -d` ile çalışan PostgreSQL **ve Mailpit** gerektirir ([[docker-ortam]]); Mailpit kapalıysa Mailpit testleri açıklayıcı hata verir. Testcontainers **kullanılmıyor**: Smart App Control imzasız DLL'i engelliyor ([[kararlar]] K-20).
