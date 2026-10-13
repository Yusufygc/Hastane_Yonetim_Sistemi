# Faz Planı ve İlerleme

Kaynak: `Fazlar.md` v3.0. Her faz uçtan uca çalışan dilim teslim eder. Mimari: [[mimari]]. Kurallar: [[git-is-akisi]]. Ödev: [[odev-gereksinimleri]].

Durum işaretleri: `[ ]` başlamadı · `[~]` sürüyor · `[x]` tamam.

## [x] Faz 0 — Depo ve Ortam (2026-10-12 tamamlandı)
`.gitignore`, `.editorconfig`, `Directory.Build.props`, Central Package Management (`Directory.Packages.props`) · `docker-compose.yml` (PostgreSQL + Mailpit) · Onion yapısında 5 proje (Domain, Application · Infrastructure, Persistence · WebAPI) + test projeleri, boş `Features/` klasörleri · otomatik kayıt altyapısı (Mediator, `IEndpoint`, EF konfigürasyon taraması) · mimari testler (NetArchTest) · endpoint-yetki tarama testi iskeleti · README · CI.
**Çıkış (sağlandı):** `docker compose up -d` + `dotnet run` ile `/health` → `Healthy`; 4 mimari test + endpoint-yetki testi + health entegrasyon testi yeşil. Ortam notları: [[docker-ortam]], [[kararlar]] (K-18, K-20).

## [x] Faz 1 — Çekirdek ve Kimlik (Auth) (2026-10-12 tamamlandı; yalnız Patient türü)
`Common` tipleri (`Entity`, `AggregateRoot`, `Result<T>`, `DomainException`), pipeline behavior'lar, `ProblemDetails` · `User` + TPT, `USER_ACCOUNT`, ilk migration (`btree_gist`) · cookie auth (kayıt/giriş/çıkış/me, stamp doğrulama) · CSRF, CORS, rate limiting, kilitleme · `ICurrentUser`, `Policies.*`, fallback policy.
**Çıkış (sağlandı):** Hasta kayıt olup giriş yapar; hasta doktor/yönetici uçlarında 403 alır. 37 birim + 9 mimari + 23 entegrasyon testi yeşil (container'da, bkz. [[docker-ortam]]). Doctor/Staff/Admin türleri Faz 2-3'te (FK'leri Faz 2 tablolarına bağlı). Bkz. [[kimlik-ve-yetki]].

### [x] Faz 1B — Risk Spike'ı (2026-10-13 tamamlandı, varsayım doğrulandı)
Çıplak `appointment` tablosu + `ex_doctor_no_overlap` / `ex_patient_no_overlap` exclusion constraint'leri (migration `AppointmentOverlapConstraints`) · EF'te `23P01` → `DataConflictException` → `409` · gerçek PostgreSQL'de 20 paralel istek → **tam 1 başarı, 19 çakışma**. 18 yeni entegrasyon + 4 birim test. Sonuç ve ayrıntı: [[randevu-kurallari]] ("Faz 1B bulguları"), [[kararlar]] K-31.

## [x] Faz 2 — Kurum ve Tesis (2026-10-13 tamamlandı)
`ORGANIZATION`, `HOSPITAL` (sınıf `HospitalBranch`), `HOSPITAL_WORKING_HOURS`, `DEPARTMENT`, `SERVICE`, **`ADMIN` türü** (süper/şube yöneticisi, kapsam claim'leri) · seed (1 org + 1 şube + örnek bölüm/hizmet + ilk süper yönetici, yapılandırmadan) · yönetici CRUD + kapsam kontrolü (kapsam dışı → 404) · pasifleştirme · anonim Catalog sorguları.
**Çıkış (sağlandı):** Yönetici şube, çalışma saati, bölüm ve süre/ücretli hizmet tanımlar; şube yöneticisi başka şubeye dokunamaz; anonim kullanıcı aktif kataloğu görür. 81 birim + 9 mimari + 91 entegrasyon test yeşil. Ayrıntı: [[kimlik-ve-yetki]], [[domain-modeli]], [[api-uc-noktalari]], [[kararlar]] K-32…K-37.

## [ ] Faz 3 — Çalışan ve Profiller
`PATIENT`, `DOCTOR`, `STAFF`, `ADMIN` · yönetici ile hesap oluşturma · **doktor-hizmet talep/onay akışı** · `DOCTOR_AVAILABILITY`, `DOCTOR_TIME_OFF` · hasta profili, TC maskeleme · katalog doktor listesi.
**Çıkış:** Hastane + doktor + onaylı hizmet + uygunluk eksiksiz tanımlanır.

## [ ] Faz 4 — Randevu (Çekirdek)
1. `GetAvailableSlots` + unit test · 2. State Pattern · 3. `APPOINTMENT` + exclusion + snapshot · 4. Komutlar (Create/Approve/Reject/Cancel/Complete) · 5. Sorgular · 6. Çakışma/eşzamanlılık testleri.
**Çıkış:** Slot → talep → doktor onayı; çakışma kodda ve DB'de engelli; başkasının randevusu 404. Bkz. [[randevu-kurallari]], [[test-stratejisi]].

## [ ] Faz 5 — Bildirimler
`EMAIL_OUTBOX`, event handler'lar, `BackgroundService` işleyici (`SKIP LOCKED`, yeniden deneme), MailKit, Mailpit doğrulaması. Bkz. [[tasarim-kaliplari]].
**Çıkış:** Talep/onay/red/iptalde Mailpit'te doğru e-posta; SMTP kapalıyken akış bozulmaz.

## [ ] Faz 6 — Sağlamlaştırma ve Backend Kapanışı
Yetki matrisi + IDOR tam kapsam · OpenAPI/Scalar · opsiyonel `Pending` zaman aşımı · performans/indeks · API Dockerfile · README/sunum özeti. **API sözleşmesi dondurulur.**

## [ ] Faz 7 — Arayüz (Three.js, scroll tabanlı)
Backend sonrası ayrıca planlanacak: animasyonlu scroll akışı, işlevsel ekranlar, cookie auth için aynı-origin/CORS+CSRF akışı, performans/erişilebilirlik dengesi.
