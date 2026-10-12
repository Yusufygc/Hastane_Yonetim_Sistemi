# GELİŞTİRME FAZLARI

> Sürüm: 3.0 (revize). Mimari ve kurallar için bkz. [plan.md](plan.md).

## Çalışma Modeli

İki kişilik ekip **ortak sorumlulukla** çalışır; kişiye özel sahiplik yoktur. Bilgi paylaşımı tam olmalı, herkes her feature'ı anlamalıdır. İş, **feature'lara (dikey dilimlere)** göre sıralanır:

- Her faz uçtan uca (domain → application → infrastructure → API → test) çalışan bir parça teslim eder.
- Her katmanda aynı feature klasörü adları kullanıldığı için aynı anda farklı feature'lar üzerinde çalışmak dosya çakışması yaratmaz.

Her faz sonunda: derlenen kod, geçen testler, `main` üzerinde çalışan uygulama.

---

## FAZ 0 — Depo ve Ortam Hazırlığı

- `.gitignore` (.NET), `.editorconfig`, `Directory.Build.props` (nullable, warnings-as-errors), Central Package Management.
- `docker-compose.yml`: PostgreSQL 16 + Mailpit.
- Çözüm iskeleti: `Hospital.Domain`, `Hospital.Application`, `Hospital.Infrastructure`, `Hospital.Api` ve test projeleri; her katmanda boş `Features/` klasörü.
- Otomatik kayıt altyapısı: handler (Mediator), `IEndpoint` taraması, EF konfigürasyon taraması.
- **Mimari testler (NetArchTest)** baştan yürürlükte: katman bağımlılıkları, feature'lar arası bağımlılık yasağı.
- **Endpoint-yetki tarama testi** iskeleti: her uç açık politika veya `AllowAnonymous` taşımalı.
- README: nasıl çalıştırılır (`docker compose up`, `dotnet run`). GitHub Actions: `dotnet build` + `dotnet test`.

**Çıkış kriteri:** `docker compose up -d` + `dotnet run` ile boş API ayağa kalkar, sağlık ucu (`/health`) DB'ye bağlanır; mimari testler yeşil.

## FAZ 1 — Çekirdek ve Kimlik (Auth)

- `Domain/Common`: `Entity`, `AggregateRoot`, `DomainEvent`, `DomainException`. `Application/Common`: `Result<T>`, Mediator pipeline behavior'ları (doğrulama, loglama, transaction).
- Global exception handling (`ProblemDetails`).
- `User` soyut sınıfı + EF **TPT** (paylaşılan PK) eşlemesi; `USER_ACCOUNT` ve ilk migration (`btree_gist` dahil). Guid v7 kimlikler.
- **Cookie kimlik doğrulama:** kayıt (yalnız hasta), giriş, çıkış, `me`, `OnValidatePrincipal` ile `security_stamp` doğrulaması, 401/403 davranışı.
- CSRF (antiforgery) ve CORS (credentials) yapılandırması, giriş rate limiting + kilitleme.
- `ICurrentUser`, politikalar (`Policies.*`), fallback policy.
- Testler: kayıt-giriş akışı, yetkisiz erişim, kilitleme, CSRF reddi, pasif kullanıcının oturumunun düşmesi.

**Çıkış kriteri:** Hasta kayıt olup giriş yapar; cookie ile korumalı uca erişir; rolü olmayan uç 403 döner.

### FAZ 1B — Risk Spike'ı (erken doğrulama)

Projenin en riskli varsayımını Faz 4'ü beklemeden doğrulayın:
- Çıplak `appointment` tablosu + exclusion constraint'ler.
- EF Core'da `23P01` hatasının yakalanıp `409`'a çevrilmesi.
- Gerçek PostgreSQL üzerinde (geçici test veritabanı) N paralel istek → tam 1 başarı testi.

Sonuç olumluysa tablo ve test Faz 4'te kalıcı hale getirilir; olumsuzsa tasarım erkenden düzeltilir.

## FAZ 2 — Kurum ve Tesis (Organizations, Hospitals, Departments, Services)

- `ORGANIZATION`, `HOSPITAL`, `HOSPITAL_WORKING_HOURS`, `DEPARTMENT`, `SERVICE` entity'leri, konfigürasyonları, migration.
- Seed: varsayılan organizasyon + tek hastane + örnek bölümler/hizmetler (tek şubeli kullanım hazır).
- Feature başına Command/Query/Handler/Validator/Endpoint (yönetici yetkisiyle), kapsam (organizasyon/şube) kontrolü.
- Pasifleştirme (silme yerine) ve referans bütünlüğü kuralları.
- Anonim **Catalog** sorguları: hastaneler, bölümler, hizmetler.
- Testler: CRUD, yetki kapsamı (şube yöneticisi başka şubeye dokunamaz), çalışma saati doğrulamaları, katalogun kişisel veri sızdırmaması.

**Çıkış kriteri:** Yönetici hastane, bölüm, çalışma saati ve süre/ücretli hizmet tanımlayabilir; anonim kullanıcı kataloğu görür.

## FAZ 3 — Çalışan ve Profiller (Patients, Doctors, Staff, Admins)

- `PATIENT`, `DOCTOR`, `STAFF`, `ADMIN` türevleri ve profil yönetimi.
- Yönetici tarafından doktor/personel/yönetici hesabı oluşturma (SuperAdmin: yönetici oluşturma).
- **Doktor-hizmet akışı (plan.md 7.5):** doktor talep eder (`PENDING`) → yönetici onaylar/reddeder; yönetici doğrudan atar (`APPROVED`); gelecekte aktif randevusu olan hizmet kaldırılamaz.
- `DOCTOR_AVAILABILITY` (haftalık uygunluk), `DOCTOR_TIME_OFF` (izin) ve doğrulamalar (hastane çalışma saati dışına taşamaz, kendi içinde çakışamaz).
- Hasta kendi profilini günceller; TC kimlik maskeleme.
- Anonim katalog: doktor listesi (yalnız `APPROVED` hizmet sunan doktorlar).
- Testler: profil kuralları, uygunluk doğrulamaları, doktor-hizmet onay akışı, yetki matrisi.

**Çıkış kriteri:** Sistemde hastane + doktor + onaylı hizmet + uygunluk verisi eksiksiz tanımlanabilir.

## FAZ 4 — Randevu (Appointments) — Projenin Çekirdeği

Sıralı alt adımlar (her biri ayrı PR):

1. **Slot sorgusu:** `GetAvailableSlots` (plan.md 7.1) + yoğun unit test.
2. **State Pattern:** durum sınıfları ve geçiş testleri (plan.md 7.4).
3. **`APPOINTMENT` tablosu + exclusion constraint'ler** (Faz 1B'den) + snapshot alanları.
4. **Komutlar:** `CreateAppointment`, `Approve`, `Reject`, `Cancel`, `Complete` + doğrulama ve sahiplik kuralları.
5. **Sorgular:** hasta geçmişi, doktor takvimi/onay bekleyenler, yönetici listesi, randevu detayı.
6. **Çakışma ve eşzamanlılık testleri** (gerçek PostgreSQL; plan.md 11'deki senaryo listesinin tamamı).

**Çıkış kriteri:** Hasta uygun slot görür ve randevu talep eder; doktor onaylar/reddeder; çakışma hem kodda hem DB'de engellenir; eşzamanlılık testi geçer; başkasının randevusuna erişim 404 döner.

## FAZ 5 — Bildirimler (Notifications)

- `EMAIL_OUTBOX`, olay handler'ları (aynı transaction'da outbox satırı), e-posta şablonları, `dedup_key` ile tekrar gönderim engeli.
- `BackgroundService` outbox işleyici (`SKIP LOCKED`, yeniden deneme, geri çekilme), MailKit.
- Mailpit ile yerel doğrulama.
- Testler: olay → outbox satırı; hata sonrası yeniden deneme; tekrar gönderim yok (idempotency).

**Çıkış kriteri:** Randevu talep/onay/red/iptal olaylarında Mailpit'te doğru e-postalar görünür; SMTP kapalıyken randevu akışı bozulmaz, mail sonra gider.

## FAZ 6 — Sağlamlaştırma ve Backend Kapanışı

- Yetki matrisi ve IDOR testlerinin tam kapsamı, API kontrat gözden geçirmesi, OpenAPI/Scalar dokümantasyonu.
- Opsiyonel: `Pending` durumundaki randevuların belirli süre içinde doktor yanıtı gelmezse otomatik düşürülmesi (arka plan işi).
- Performans bakışı (indeksler, sorgu planı), log/correlation id.
- API için Dockerfile ve compose'a eklenmesi.
- README ve sunum için mimari özet.

**Çıkış kriteri:** Backend, arayüzsüz yalnızca API üzerinden tüm ödev gereksinimlerini karşılar. Frontend başlamadan önce **API sözleşmesi dondurulur**.

## FAZ 7 — Arayüz (Three.js, scroll tabanlı akış)

*Backend bittikten sonra ayrıca planlanacak.* Ön notlar:
- Kaydırdıkça içeriğin değiştiği animasyonlu giriş/tanıtım akışı (Three.js; React Three Fiber + scroll kontrolü veya GSAP ScrollTrigger değerlendirilecek).
- Cookie auth nedeniyle arayüz ve API'nin aynı site/origin altında çalışması (veya doğru CORS + credentials + CSRF token akışı) baştan planlanır.
- Işlevsel ekranlar: klinik/hizmet seçimi, doktor ve slot listesi, randevu talebi, hasta geçmişi, doktor onay paneli ve hizmet talepleri, yönetici paneli.
- 3B animasyon ile erişilebilirlik/performans dengesi (düşük cihazlar için sadeleştirilmiş mod).

---

## Git ve İş Birliği Kuralları

1. **Branch:** `main` korunur, doğrudan commit yok. Çalışma dalları: `feature/<kısa-ad>`, `fix/<kısa-ad>`. Her biri PR ile birleşir; diğer kişi inceler.
2. **Commit:** Küçük, anlamlı, Conventional Commits (`feat(appointments): slot üretimi`). Ödev şartı: farklı günlere yayılmış en az 10 commit; hedef çok daha fazlası. Bir fazı tek commit'te toplamayın.
3. **Çakışmayı azaltma:**
   - Handler, endpoint ve EF konfigürasyonları taramayla otomatik kaydolur; `Program.cs` ve `AppDbContext` yeni feature için değişmez.
   - Her feature kendi DTO/Request/Response sınıflarını taşır; feature'lar arası paylaşım yalnızca soyutlama arayüzleriyle.
   - Entity konfigürasyonları sınıf başına ayrı dosyada (`IEntityTypeConfiguration`).
   - **Migration çakışması:** Aynı anda iki kişi migration üretirse model snapshot çakışır. Kural: migration üreten kişi PR'ını hızla birleştirir; diğeri `main`'i çekip kendi migration'ını **yeniden üretir**.
4. **Sık senkronizasyon:** Günde en az bir kez `main`'i çalışma dalına alın.
5. **CI:** GitHub Actions ile `dotnet build` + `dotnet test` (entegrasyon testleri için CI'da PostgreSQL service container tanımlıdır). Kırık dal birleşmez.
