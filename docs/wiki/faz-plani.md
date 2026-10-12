# Faz Planı ve İlerleme

Kaynak: `Fazlar.md` v3.0. Her faz uçtan uca çalışan dilim teslim eder. Mimari: [[mimari]]. Kurallar: [[git-is-akisi]]. Ödev: [[odev-gereksinimleri]].

Durum işaretleri: `[ ]` başlamadı · `[~]` sürüyor · `[x]` tamam.

## [x] Faz 0 — Depo ve Ortam (2026-10-12 tamamlandı)
`.gitignore`, `.editorconfig`, `Directory.Build.props`, Central Package Management (`Directory.Packages.props`) · `docker-compose.yml` (PostgreSQL + Mailpit) · 4 proje + test projeleri, boş `Features/` klasörleri · otomatik kayıt altyapısı (Mediator, `IEndpoint`, EF konfigürasyon taraması) · mimari testler (NetArchTest) · endpoint-yetki tarama testi iskeleti · README · CI.
**Çıkış (sağlandı):** `docker compose up -d` + `dotnet run` ile `/health` → `Healthy`; 4 mimari test + endpoint-yetki testi + health entegrasyon testi yeşil. Ortam notları: [[docker-ortam]], [[kararlar]] (K-18, K-20).

## [ ] Faz 1 — Çekirdek ve Kimlik (Auth)
`Common` tipleri (`Entity`, `AggregateRoot`, `Result<T>`, `DomainException`), pipeline behavior'lar, `ProblemDetails` · `User` + TPT, `USER_ACCOUNT`, ilk migration (`btree_gist`) · cookie auth (kayıt/giriş/çıkış/me, stamp doğrulama) · CSRF, CORS, rate limiting, kilitleme · `ICurrentUser`, `Policies.*`, fallback policy.
**Çıkış:** Hasta kayıt olup giriş yapar; rolü olmayan uç 403. Bkz. [[kimlik-ve-yetki]].

### [ ] Faz 1B — Risk Spike'ı
Çıplak `appointment` + exclusion constraint · EF'te `23P01` → `409` · gerçek PostgreSQL üzerinde N paralel istek → tam 1 başarı. Bkz. [[randevu-kurallari]].

## [ ] Faz 2 — Kurum ve Tesis
`ORGANIZATION`, `HOSPITAL`, `HOSPITAL_WORKING_HOURS`, `DEPARTMENT`, `SERVICE` · seed (1 org + 1 hastane) · yönetici CRUD + kapsam kontrolü · pasifleştirme · anonim Catalog sorguları.
**Çıkış:** Yönetici kurum/hizmet tanımlar; anonim kullanıcı kataloğu görür.

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
