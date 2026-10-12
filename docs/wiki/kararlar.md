# Karar Kaydı

Mimari kararlar, gerekçeleri ve reddedilen alternatifler. Yeni karar en alta eklenir. Kronoloji: [[log]]. Uygulama detayları: [[mimari]], [[teknoloji-yigini]].

| # | Karar | Gerekçe / Reddedilen |
|---|---|---|
| K-01 | PostgreSQL 16, Docker ile | Exclusion constraint + `tstzrange` ile DB seviyesinde çakışma koruması ([[randevu-kurallari]]). SQL Server'da yok. |
| K-02 | Clean Architecture, 4 proje + her katmanda `Features/` klasörleri | Kullanıcı isteği: handler'lar tek yerde değil, feature bazlı. **Reddedildi:** modül başına csproj + modül başına DbContext (15+ proje, modüller arası FK kaybı). Plan.md v2'deki "modüler monolit" terimi bırakıldı. |
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
| K-18 | Hedef .NET 9; geliştirme makinesinde SDK 10.0.401 + runtime 9.0.20 | **Çözüldü:** SDK 10 `net9.0` hedefini derler, runtime 9 çalıştırır. `global.json` kullanılmıyor (CI SDK 9.0.x ile de derler). EF/ASP.NET paketleri 9.x'e sabit (10.x net9 ile uyumsuz). Bkz. [[docker-ortam]]. |
| K-19 | Asistan kullanıcı istemedikçe commit/push yapmaz | Genel çalışma kuralı ([[git-is-akisi]]). |
| K-20 | Testcontainers **kullanılmıyor**; entegrasyon testleri compose PostgreSQL'inde geçici veritabanı açar | Windows Smart App Control imzasız `Testcontainers.PostgreSql.dll` yüklemesini engelledi (0x800711C7). Güvenlik ayarı devre dışı bırakılmadı. Yerine `PostgresFixture` (Npgsql ile `CREATE/DROP DATABASE`); CI'da PostgreSQL service container. Bkz. [[test-stratejisi]]. |
| K-21 | FluentAssertions 7.2.2'ye sabit | 8.x ticari lisanslı; 7.x Apache. |
| K-22 | Paket sürümleri Central Package Management ile `Directory.Packages.props`'ta | Sürüm kayması ve proje başına tekrar önlenir. |
