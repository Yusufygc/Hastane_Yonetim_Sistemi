# Karar Kaydı

Mimari kararlar, gerekçeleri ve reddedilen alternatifler. Yeni karar en alta eklenir. Kronoloji: [[log]]. Uygulama detayları: [[mimari]], [[teknoloji-yigini]].

| # | Karar | Gerekçe / Reddedilen |
|---|---|---|
| K-01 | PostgreSQL 16, Docker ile | Exclusion constraint + `tstzrange` ile DB seviyesinde çakışma koruması ([[randevu-kurallari]]). SQL Server'da yok. |
| K-02 | ~~Clean Architecture, 4 proje~~ (K-29 ile Onion 5 proje oldu) + her katmanda `Features/` klasörleri | Kullanıcı isteği: handler'lar tek yerde değil, feature bazlı. **Reddedildi:** modül başına csproj + modül başına DbContext (15+ proje, modüller arası FK kaybı). Plan.md v2'deki "modüler monolit" terimi bırakıldı. |
| K-03 | `Mediator` (martinothamar) | MediatR v13+ ticari lisanslı. |
| K-04 | Cookie authentication | Kullanıcı kararı. **Reddedildi:** JWT + refresh token. Sonuç: CSRF/CORS önlemleri, `security_stamp` ([[kimlik-ve-yetki]]). |
| K-05 | `Guid` v7 kimlikler | `/appointments/57` tahmini ve IDOR'u kolaylaştırır; v7 sıralı, indeks dostu. |
| K-06 | `Result<T>` (beklenen hata) + `DomainException` (invariant) | Handler'larda try/catch yok; tek middleware `ProblemDetails`. |
| K-07 | Minimal API | Kullanıcı onayı; feature başına `IEndpoint`. |
| K-08 | Tek `AppDbContext`, gerçek FK | Tek veritabanı, 2 kişilik ekip; bütünlük DB'de. |
| K-09 | Gerçek TPT (paylaşılan PK) | Eski ER (ayrı id + user_account_id) kalıtım değildi ([[domain-modeli]]). |
| K-10 | Çakışma: uygulama + DB exclusion constraint | Eşzamanlı isteklerde yalnız uygulama kontrolü yetmez. |
| K-11 | Transactional Outbox ile e-posta | Çökmede bildirim kaybını önler ([[tasarim-kaliplari]]). |
| K-12 | Doktor-hizmet ataması **yönetici onaylı** | Kullanıcı kararı: doktor talep eder, yönetici onaylar; yönetici doğrudan da atayabilir ([[randevu-kurallari]]). |
| K-13 | Sabit süreli hizmet, 15 dk ızgara | Kullanıcı kararı: sabit süre. |
| K-14 | `PENDING` randevu slotu rezerve eder | Çifte rezervasyonu önler. |
| K-15 | Katalog anonim; randevu için giriş şart | Kullanıcı kararı. |
| K-16 | `ORGANIZATION` her zaman var; tek şube = 1 org + 1 hastane | Çok şube altyapısı, tek şube zorunluluğu değil. |
| K-17 | Rolsüz URL, `me` kalıbı; rol cookie'den | Kullanıcı kararı ([[api-uc-noktalari]]). |
| K-18 | Hedef .NET 9; `global.json` SDK 9.0.318'e sabitler (`rollForward: latestFeature`) | **Çözüldü (2026-10-12):** .NET 9 SDK kuruldu; SDK 10 yan yana duruyor ama bu repoda 9 kullanılır. EF/ASP.NET paketleri 9.x'e sabit (10.x net9 ile uyumsuz). Bkz. [[docker-ortam]]. |
| K-19 | Asistan kullanıcı istemedikçe commit/push yapmaz | Genel çalışma kuralı ([[git-is-akisi]]). |
| K-20 | Testcontainers **kullanılmıyor**; entegrasyon testleri compose PostgreSQL'inde geçici veritabanı açar | Windows Smart App Control imzasız `Testcontainers.PostgreSql.dll` yüklemesini engelledi (0x800711C7). Güvenlik ayarı devre dışı bırakılmadı. Yerine `PostgresFixture` (Npgsql ile `CREATE/DROP DATABASE`); CI'da PostgreSQL service container. Bkz. [[test-stratejisi]]. |
| K-21 | Assertion kütüphanesi **AwesomeAssertions** (Apache, FluentAssertions 7 fork'u) | Kullanıcı kararı: FluentAssertions 8.x ticari lisanslı. Aynı `Should()` API'si. (FluentValidation ayrı bir kütüphane: istek doğrulama, ücretsiz, kullanımda.) |
| K-23 | Derleme/test güvenilir yolu: `scripts/test-docker.ps1` (Linux SDK container) | SAC, yerel derlenmiş `Hospital.Api.dll`'i engelledi; ayar kapatılmadı ([[docker-ortam]]). |
| K-24 | Transaction pipeline behavior yok; pipeline = loglama → doğrulama; tüm mesajlar `Result` döner | Handler tek `SaveChanges` ile zaten atomik; fazladan katman gereksiz ([[tasarim-kaliplari]]). |
| K-25 | Giriş hataları tek tip (`auth.invalid_credentials`); kilit/pasif/bilinmeyen aynı yanıt | Hesap varlığı sızdırılmaz ([[kimlik-ve-yetki]]). |
| K-26 | CSRF belirteci tüm `/api` yazma isteklerinde zorunlu (login/register dahil); kimlik değişince yenilenir | Cookie auth + login CSRF koruması. |
| K-27 | Faz 1'de yalnız `Patient` türü; Doctor/Staff/Admin Faz 2-3 | FK'leri (bölüm, organizasyon) Faz 2 tablolarına bağlı. |
| K-28 | EF migration'ları `Persistence/Migrations`; `dotnet-ef` yerel araç (`.config/dotnet-tools.json`; komut: `dotnet ef migrations add <Ad> --project src/Infrastructure/Hospital.Persistence --startup-project src/Infrastructure/Hospital.Persistence --output-dir Migrations`); dev/test'te `Database:MigrateOnStartup=true` | Tek komutla çalışan geliştirme ortamı. |
| K-29 | **Onion mimari**, 5 proje: Core (Domain, Application), Infrastructure (Infrastructure, Persistence), Presentation (WebAPI); fiziksel `src/Core`, `src/Infrastructure`, `src/Presentation` | Kullanıcı kararı (referans görsel). EF/veri erişimi ayrı `Persistence` projesine alındı; Application EF'i bilmez. İç klasörler (Interfaces, Wrappers, Exceptions, Dto, Parameters; Domain'de Common/Entities) görseldeki adlara uyar. `Hospital.Api` → `Hospital.WebAPI`. K-02'deki 4 proje düzeninin yerini alır ([[mimari]]). |
| K-30 | DI kuralı: her katman `Extensions/<Katman>Ext.cs` içinde `IServiceCollection` extension'ı `Add<Katman>` ile kaydolur; `Program.cs` yalnızca çağırır | Kullanıcı kararı. Katman kapsülleme, ince Program.cs, çakışmasız ekip çalışması. `ServiceRegistration.cs` (kök) kaldırıldı; WebAPI kayıtları Program.cs'ten `WebApiExt`'e taşındı; kural mimari testle korunur ([[di-kurallari]]). |
| K-31 | Faz 1B: çakışma kısıtları (`ex_doctor_no_overlap`, `ex_patient_no_overlap`) migration'da ham SQL; `appointment.doctor_id` şimdilik FK'siz; durum CHECK + yarı açık aralık `[)` | Spike varsayımı doğrulandı (20 paralel istek → 1 başarı). Doktor tablosu Faz 3'te geldiğinde FK eklenir; EF kısıtı ifade edemez, ham SQL gerekir ([[randevu-kurallari]]). |
| K-22 | Paket sürümleri Central Package Management ile `Directory.Packages.props`'ta | Sürüm kayması ve proje başına tekrar önlenir. |
