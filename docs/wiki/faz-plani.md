# Faz Planı ve İlerleme

Kaynak: `Fazlar.md` v3.0. Her faz uçtan uca çalışan dilim teslim eder. Mimari: [[mimari]]. Kurallar: [[git-is-akisi]]. Ödev: [[odev-gereksinimleri]].

Durum işaretleri: `[ ]` başlamadı · `[~]` sürüyor · `[x]` tamam.

## [x] Faz 0 — Depo ve Ortam (2026-10-12 tamamlandı)
`.gitignore`, `.editorconfig`, `Directory.Build.props`, Central Package Management (`Directory.Packages.props`) · `docker-compose.yml` (PostgreSQL + Mailpit) · Onion yapısında 5 proje (Domain, Application · Infrastructure, Persistence · WebAPI) + test projeleri, boş `Features/` klasörleri · otomatik kayıt altyapısı (Mediator, `IEndpoint`, EF konfigürasyon taraması) · mimari testler (NetArchTest) · endpoint-yetki tarama testi iskeleti · README · CI.
**Çıkış (sağlandı):** `docker compose up -d` + `dotnet run` ile `/health` → `Healthy`; 4 mimari test + endpoint-yetki testi + health entegrasyon testi yeşil. Ortam notları: [[docker-ortam]], [[kararlar]] (K-18, K-20).

## [x] Faz 1 — Çekirdek ve Kimlik (Auth) (2026-10-12 tamamlandı; yalnız Patient türü)
`Common` tipleri (`Entity`, `AggregateRoot`, `Result<T>`, `DomainException`), pipeline behavior'lar, `ProblemDetails` · `User` + TPT, `USER_ACCOUNT`, ilk migration (`btree_gist`) · kimlik doğrulama (kayıt/giriş/çıkış/me; **2026-10-13'te cookie'den JWT'ye geçildi**: erişim + refresh token, rotation, stamp doğrulama) · CORS, rate limiting, kilitleme · `ICurrentUser`, `Policies.*`, fallback policy.
**Çıkış (sağlandı):** Hasta kayıt olup giriş yapar; hasta doktor/yönetici uçlarında 403 alır. 37 birim + 9 mimari + 23 entegrasyon testi yeşil (container'da, bkz. [[docker-ortam]]). Doctor/Staff/Admin türleri Faz 2-3'te (FK'leri Faz 2 tablolarına bağlı). Bkz. [[kimlik-ve-yetki]].

### [x] Faz 1B — Risk Spike'ı (2026-10-13 tamamlandı, varsayım doğrulandı)
Çıplak `appointment` tablosu + `ex_doctor_no_overlap` / `ex_patient_no_overlap` exclusion constraint'leri (migration `AppointmentOverlapConstraints`) · EF'te `23P01` → `DataConflictException` → `409` · gerçek PostgreSQL'de 20 paralel istek → **tam 1 başarı, 19 çakışma**. 18 yeni entegrasyon + 4 birim test. Sonuç ve ayrıntı: [[randevu-kurallari]] ("Faz 1B bulguları"), [[kararlar]] K-31.

## [x] Faz 2 — Kurum ve Tesis (2026-10-13 tamamlandı)
`ORGANIZATION`, `HOSPITAL` (sınıf `HospitalBranch`), `HOSPITAL_WORKING_HOURS`, `DEPARTMENT`, `SERVICE`, **`ADMIN` türü** (süper/şube yöneticisi, kapsam claim'leri) · seed (1 org + 1 şube + örnek bölüm/hizmet + ilk süper yönetici, yapılandırmadan) · yönetici CRUD + kapsam kontrolü (kapsam dışı → 404) · pasifleştirme · anonim Catalog sorguları.
**Çıkış (sağlandı):** Yönetici şube, çalışma saati, bölüm ve süre/ücretli hizmet tanımlar; şube yöneticisi başka şubeye dokunamaz; anonim kullanıcı aktif kataloğu görür. 81 birim + 9 mimari + 91 entegrasyon test yeşil. Ayrıntı: [[kimlik-ve-yetki]], [[domain-modeli]], [[api-uc-noktalari]], [[kararlar]] K-32…K-37.

## [x] Faz 3 — Çalışan ve Profiller (2026-10-20 tamamlandı)
`DOCTOR`, `STAFF` türleri (+ mevcut `PATIENT`, `ADMIN`) · yönetici ile hesap oluşturma (doktor/personel/yönetici) · **doktor-hizmet talep/onay akışı** · `DOCTOR_AVAILABILITY`, `DOCTOR_TIME_OFF` (kodda + DB exclusion constraint) · hasta profili, TC maskeleme · katalog doktor listesi · parola değiştirme · kullanıcı pasifleştirme/aktifleştirme · seed örnek doktor.
**Çıkış (sağlandı):** Yönetici doktor açar; doktor hizmet talep eder, yönetici onaylar; doktor şube saatleri içinde uygunluk ve izin tanımlar; anonim katalog yalnız onaylı hizmetli doktorları gösterir; şube yöneticisi başka şubenin doktor/personel/talebine dokunamaz (404). 162 birim + 9 mimari + 215 entegrasyon test yeşil. Ayrıntı: [[domain-modeli]], [[veritabani]], [[kimlik-ve-yetki]], [[api-uc-noktalari]], [[kararlar]] K-40…K-48. Faz 4'e bırakılanlar: `appointment.doctor_id` FK'si, hizmet/uygunluk/kullanıcı pasifleştirmede gelecek randevu denetimi.

## [x] Faz 4 — Randevu (Çekirdek) (2026-10-21 tamamlandı)
1. `SlotCalculator` (saf, DST'ye duyarlı) + `GetAvailableSlots` · 2. State Pattern (`AppointmentState` + 5 durum) · 3. `APPOINTMENT` genişletildi: FK'ler, snapshot, `xmin` eşzamanlılık belirteci (migration `AppointmentCore`) · 4. Komutlar: Create/Approve/Reject/Cancel/Complete · 5. Sorgular: slotlar, detay, hasta/doktor/yönetici listeleri, doktor takvimi · 6. Çakışma/eşzamanlılık testleri (HTTP seviyesinde 20 paralel istek) · 7. Faz 3'ten devreden gelecek-randevu denetimleri (hizmet kaldırma, izin ekleme, hesap pasifleştirme).
**Çıkış (sağlandı):** Anonim slot sorgusu → hasta talebi (PENDING, snapshot) → doktor onayı/reddi → iptal/tamamlama; çakışma kodda ve DB'de engelli; başkasının randevusu 404; hastanın iptal süre sınırı. 251 birim + 9 mimari + 260 entegrasyon test yeşil. Bkz. [[randevu-kurallari]], [[domain-modeli]], [[veritabani]], [[api-uc-noktalari]], [[kararlar]] K-49…K-56, [[test-stratejisi]]. Faz 5'e bırakılanlar: olay dağıtımı + e-posta outbox; Faz 6: sayfalama.

## [x] Faz 5 — Bildirimler (2026-10-23 tamamlandı)
`EMAIL_OUTBOX` (migration `EmailOutbox`), olay dağıtımı SaveChanges içinde (`IDomainEventDispatcher`), randevu e-posta dinleyicileri (talep/onay/red/iptal), Türkçe şablonlar, `dedup_key` ile tekrar önleme, `BackgroundService` outbox işleyici (`FOR UPDATE SKIP LOCKED`, üstel geri çekilme, 5 denemede `FAILED`), MailKit 4.18.1, Mailpit ile doğrulama, smoke test bölüm 8.
**Çıkış (sağlandı):** Talep/onay/red/iptalde Mailpit'te doğru e-postalar görünür; SMTP kapalıyken (gerçek kapalı port) randevu akışı bozulmaz, e-posta bekler ve SMTP gelince gider; aynı olay için tek e-posta; çoklu işleyici çift göndermez. 275 birim + 9 mimari + 279 entegrasyon test yeşil. Bkz. [[bildirimler]], [[tasarim-kaliplari]], [[kararlar]] K-57…K-61, [[test-stratejisi]].

## [x] Faz 6 — Sağlamlaştırma ve Backend Kapanışı (2026-10-25 tamamlandı)
Yetki matrisi (67 uç × 6 kimlik) + IDOR matrisi · OpenAPI/Scalar, tipli yanıtlar, **dondurulmuş sözleşme** (`docs/api/openapi.v1.json`) · `Pending` zaman aşımı arka plan işi · indeks gözden geçirmesi + `QueryPlanTests` · `X-Correlation-Id` + log · sayfalama (K-56 borcu; başlıkla) · T.C. kimlik no şifreleme (KVKK, AES-GCM + kör indeks, migration `Hardening`) · API `Dockerfile` + `docker-compose.app.yml` · zorunlu ayar doğrulaması · README + [[sunum-ozeti]] + [[operasyon]].
**Çıkış (sağlandı):** 301 birim + 9 mimari + 350 entegrasyon test yeşil; API imajı derlenip container'da çalıştırıldı (root olmayan kullanıcı, `/health` sağlıklı) ve `smoke-test.sh` canlı **57/57** (bölüm 6–8 ve Mailpit e-postaları dahil). Sözleşme dondu: Faz 7 arayüzü yalnız bu dosyaya dayanır. Bkz. [[kararlar]] K-62…K-70, [[operasyon]], [[test-stratejisi]], [[veritabani]], [[kimlik-ve-yetki]], [[api-uc-noktalari]].

## [ ] Faz 7 — Arayüz (Three.js, scroll tabanlı akış) — **ilk dilim tamamlandı (2026-10-26)**
Kararlar: [[kararlar]] K-71…K-75, ayrıntı [[arayuz]].
- [x] `web/` iskeleti (React + TS + Vite), OpenAPI'den üretilen tipler, CI `web` işi
- [x] JWT oturumu: bellekte erişim + `sessionStorage` yenileme, tek uçuşlu yenileme
- [x] Kaydırmalı 3B tanıtım (çift sarmal), sade mod, tembel yükleme
- [x] Kayıt/giriş, randevu sihirbazı (şube→hizmet→doktor→tarih/saat→talep), randevularım (filtre, sayfalama, iptal)
- [x] 21 Vitest testi (http istemcisi, yardımcılar, uygulama akışları)
- [x] Doktor paneli (2026-10-27): bekleyen talepleri onay/red, takvim, hizmet talepleri, uygunluk/izin, profil — [[arayuz]], K-76…K-78; 30 Vitest testi
- [ ] Yönetici paneli: şube/bölüm/hizmet, doktor/personel, hizmet talebi onayı
- [ ] Web imajı (statik dosya + ters vekil) ve compose'a ekleme; erişilebilirlik denetimi
