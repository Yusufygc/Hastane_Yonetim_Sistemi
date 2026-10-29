# Arayüz (Faz 7)

`web/` klasörü: React 19 + TypeScript + Vite; 3B için three.js (React Three Fiber). Backend sözleşmesi [[operasyon]] ("API sözleşmesi") dosyasından türetilir. Karar gerekçeleri: [[kararlar]] K-71…K-75. Yetki modeli: [[kimlik-ve-yetki]]. Uç noktalar: [[api-uc-noktalari]].

## İlk dilim (tamamlandı)
Tanıtım sayfası, kayıt/giriş, randevu sihirbazı, randevularım. **İkinci dilim: doktor paneli** (aşağıda). **Üçüncü dilim: yönetim paneli** (aşağıda). Sıradaki: web imajı ([[faz-plani]]).

| Rota | Ekran | Erişim |
|---|---|---|
| `/` | Kaydırdıkça değişen 3B tanıtım (4 bölüm) | anonim |
| `/giris`, `/kayit` | Giriş / hasta kaydı (alan bazlı hata gösterimi) | anonim |
| `/randevu-al` | Sihirbaz: şube → hizmet → doktor → tarih → saat → özet/talep | katalog anonim; talep göndermek giriş ister |
| `/randevularim` | Hasta randevuları: durum filtresi, sayfalama (`X-Total-Count`), iptal nedeniyle iptal | giriş gerekir (yalnız görsel kilit) |

## Giriş ve roller (demo rehberi)
Adres: arayüz `http://localhost:5173` (geliştirme) veya `http://localhost:8081` (compose'ta nginx). Sağ üstteki **Giriş** / **Kayıt ol** düğmeleri. Roller giriş yanıtından okunur; giriş sonrası kullanıcı **rolün ana ekranına** düşer.

| Rol | Hesap | Nasıl girilir | Girişten sonra | Erişebildiği sayfalar |
|---|---|---|---|---|
| **Hasta** | Kendin açarsın (parola unutulursa **Şifremi unuttum**) | `/kayit` → ad, soyad, e-posta, parola, **geçerli T.C. kimlik no**, doğum tarihi → kayıt olunca otomatik giriş yapılır | `/randevu-al` (kayıttan) veya `/randevularim` (girişten) | Randevu al, Randevularım |
| **Doktor** | Seed örnek doktor: `doktor@hastane.local` / `Doktor12345x` (Ayşe Yılmaz, Dahiliye) | `/giris` | `/doktor` | Doktor paneli (randevular, takvim, hizmetlerim, uygunluk ve izin, profil) |
| **Yönetici (süper)** | Seed süper yönetici: `admin@hastane.local` / `Admin12345x` | `/giris` | `/yonetim` | Yönetim paneli (randevular, hizmet talepleri, doktorlar, personel, şubeler, bölümler, hizmetler) |

- **Seed hesapları** yalnız geliştirmede açılır (`appsettings.Development.json` → `Seed:SuperAdmin`, `Seed:SampleDoctor`, `Seed:SampleData=true`); üretimde ortam değişkeniyle verilir ve örnek doktor açılmaz ([[operasyon]]). Compose'ta örnek doktor için `.env`'e `SEED_SAMPLE_DATA=true`, `SEED_SAMPLE_DOCTOR_EMAIL`, `SEED_SAMPLE_DOCTOR_PASSWORD` yazılır ve **boş veritabanıyla** başlatılır.
- **Hasta için geçerli T.C. kimlik no:** 11 hane, ilk hane 0 değil; 10. hane = ((1,3,5,7,9. hanelerin toplamı × 7) − (2,4,6,8. hanelerin toplamı)) mod 10; 11. hane = ilk 10 hanenin toplamı mod 10. Örnek geçerli numara: `10000000146` (aynı numara ikinci kez kaydedilemez; yeni hasta için `scripts/smoke-test.sh` rastgele geçerli numara üretir). Parola kuralı: [[hata-yonetimi]] doğrulama mesajları (en az 8 karakter, harf + rakam).
- **Doktor ve yönetici hesaplarını yönetici açar** (`/yonetim/doktorlar`, `/yonetim/personel`): başlangıç parolasını yönetici verir, kullanıcı girişten sonra `change-password` ile değiştirir ([[kararlar]] K-40). Hasta kendini doktor/yönetici yapamaz; kayıt ucu yalnız hasta oluşturur.
- **Şube yöneticisi:** süper yönetici `/yonetim/subeler` ve API ile şube yöneticisi hesabı açabilir (`POST /api/admins`); şube yöneticisi yalnız kendi şubesini görür, şube/kurum ekleyemez. Arayüzde yönetici hesabı oluşturma ekranı yok ([[arayuz]] sınırlar).
- **Parolamı unuttum:** giriş sayfasındaki **Şifremi unuttum** → `/sifremi-unuttum` (e-posta; yanıt her zaman "adres kayıtlıysa bağlantı gönderdik") → e-postadaki bağlantı `/sifre-sifirla?token=…` → yeni parola (tekrarıyla) → "Parolanız değiştirildi" → giriş. Belirteç ilk açılışta okunup **adres çubuğundan ve geçmişten silinir** (`replace`); geçersiz/süresi dolmuş/kullanılmış bağlantıda "Bağlantı geçersiz → Yeni bağlantı iste" çıkar. Yerelde e-postayı Mailpit'ten (`http://localhost:8025`) açın. Akışın güvenlik ayrıntısı: [[kimlik-ve-yetki]] "Parola sıfırlama", karar [[kararlar]] K-86…K-88.
- **Çıkış:** sağ üstteki **Çıkış**; refresh token iptal edilir. Sekme kapanınca oturum biter (yenileme belirteci `sessionStorage`'da, [[kararlar]] K-71).

### Yetkilendirme denemesi (2026-10-29)
Her rolün başka rolün sayfasına/uçlarına girme denemesi hem otomatik (Vitest `RouteAccess.test.tsx`, API tarafı `EndpointPolicyMatrixTests`/`IdorMatrixTests`) hem Chrome'da canlı yapıldı:

| Deneme | Arayüz sonucu | API sonucu |
|---|---|---|
| Hasta → `/doktor` | "Bu sayfaya erişiminiz yok" | `GET /api/doctors/me`, `/doctors/me/appointments`, randevu onaylama → **403** |
| Hasta → `/yonetim` | "Bu sayfaya erişiminiz yok" | `GET /api/hospitals`, `/doctors`, `/appointments`, `/organization`, `/doctor-service-requests`, kullanıcı pasifleştirme → **403** |
| Doktor → `/yonetim` | "Bu sayfaya erişiminiz yok" | yönetici uçlarının hepsi → **403** |
| Doktor → `/randevularim` | "Bu sayfaya erişiminiz yok" | `GET /api/patients/me`, `/patients/me/appointments`, `POST /api/appointments` → **403** |
| Yönetici → `/doktor`, `/randevularim` | "Bu sayfaya erişiminiz yok" | `GET /api/doctors/me*`, `/patients/me*`, `POST /api/appointments` → **403** |
| Anonim → `/doktor`, `/yonetim`, `/randevularim` | Giriş sayfasına yönlendirilir | korumalı uçlar **401** |
| Her rol kendi uçlarına | sayfa açılır | **200** |

Kilit arayüzde yalnız görseldir; yanlış rolle girilen sayfa hiçbir korumalı uca istek atmaz, atılsa bile backend 403/404 verir ([[kimlik-ve-yetki]]).

## Doktor paneli (ikinci dilim)
Rota ağacı `/doktor/*`, `RoleRoute role="DOCTOR"` ile görsel olarak kilitli (hasta girerse açıklayıcı mesaj; asıl yetki backend `Doctor` politikasıdır). Giriş sonrası doktor doğrudan panele düşer, üst menüde doktor için yalnız "Doktor paneli" görünür (hasta menüsü gizlenir) — [[kararlar]] K-76.

| Rota | Ekran | Kullanılan uçlar |
|---|---|---|
| `/doktor` | **Randevular**: durum filtresi (varsayılan *Bekleyen*), sayfalama; bekleyen → **Onayla / Reddet** (neden zorunlu), onaylı → **Tamamlandı** (yalnız randevu başladıysa) / **İptal** (neden zorunlu) | `GET /api/doctors/me/appointments`, `POST /api/appointments/{id}/approve｜reject｜complete｜cancel` |
| `/doktor/takvim` | Haftalık takvim (önceki/sonraki hafta), güne göre gruplu onaylı randevular, kartta aynı işlemler | `GET /api/doctors/me/schedule?from&to` |
| `/doktor/hizmetlerim` | Hizmet talepleri ve durumları; şubenin henüz alınmamış hizmetlerinden **talep et**; bekleyen/reddedilen talebi geri çek (onaylıyı yalnız yönetici kaldırır — K-41) | `GET/POST /api/doctors/me/services`, `DELETE …/{serviceId}`, `GET /api/catalog/hospitals/{id}/services` |
| `/doktor/uygunluk` | Haftalık **uygunluk** penceresi ekle/sil (gün, saat, geçerlilik) ve **izin** ekle/sil | `GET/POST/DELETE /api/doctors/me/availability`, `…/time-off` |
| `/doktor/profil` | Salt okunur kimlik bilgileri; uzmanlık + telefon düzenleme | `GET/PUT /api/doctors/me` |

Ortak parçalar: `components/ReasonForm` (gerekçe isteyen küçük form; iptal/red, hasta tarafı iptali de kullanır), `components/RoleRoute`, `pages/doctor/AppointmentCard` (kart + durumuna göre işlemler), `lib/reload` (işlemden sonra listeyi yenileme sayacı).

Davranış notları: işlem hataları (örn. `409 appointment.invalid_transition`, izin/uygunluk çakışması) ProblemDetails mesajı ve `traceId` ile **kartın içinde** gösterilir; izin saatleri tarayıcı saat dilimindedir (`datetime-local` → UTC ISO); takvim aralığı yerel Pazartesi 00:00'dan 7 gündür.

## Yönetim paneli (üçüncü dilim)
Rota ağacı `/yonetim/*`, `RoleRoute role="ADMIN"` ile görsel olarak kilitli; giriş sonrası yönetici doğrudan panele düşer, menüde yalnız "Yönetim paneli" görünür ([[kararlar]] K-79). `AdminProvider` erişilebilir şubeleri yükler (`GET /api/hospitals`: şube yöneticisi yalnız kendi şubesini görür), **seçili şubeyi** (üstteki seçici, `sessionStorage`) tüm listelere uygular ve süper yöneticiyi `GET /api/organization`'ın 200 dönmesinden anlar (şube yöneticisinde 403 → "Şube yöneticisi" rozeti, süper yönetici işlevleri gizlenir).

| Rota | Ekran | Kullanılan uçlar |
|---|---|---|
| `/yonetim` | **Randevular** (seçili şube): durum filtresi, sayfalama, **nedenli iptal** | `GET /api/appointments`, `POST …/cancel` |
| `/yonetim/hizmet-talepleri` | Doktor hizmet talepleri: Bekleyen/Onaylı/Reddedilen, **onayla / reddet** | `GET /api/doctor-service-requests`, `POST /api/doctors/{d}/services/{s}/approve｜reject` |
| `/yonetim/doktorlar` | Doktor listesi, **oluştur** (hesap + başlangıç parolası), **düzenle**, **pasifleştir/aktifleştir** | `GET/POST /api/doctors`, `PUT /api/doctors/{id}`, `POST /api/users/{id}/deactivate｜activate` |
| `/yonetim/personel` | Personel listesi, oluştur, pasifleştir/aktifleştir | `GET/POST /api/staff`, `POST /api/users/…` |
| `/yonetim/subeler` | Şube bilgileri, **haftalık çalışma saatleri** (gün başına tek aralık, işaretsiz gün kapalı), pasifleştirme; **süper yönetici:** yeni şube + kurum adı | `GET/POST/PUT /api/hospitals`, `PUT …/working-hours`, `POST …/deactivate`, `GET/PUT /api/organization` |
| `/yonetim/bolumler` | Bölüm ekle, yeniden adlandır, pasifleştir (aktif hizmeti olan pasifleştirilemez) | `GET/POST /api/hospitals/{id}/departments`, `PUT/POST /api/departments/{id}…` |
| `/yonetim/hizmetler` | Hizmet ekle/düzenle (ad, bölüm, süre, ücret, para birimi), pasifleştir | `GET/POST/PUT /api/services`, `POST …/deactivate` |

Ortak parçalar: `components/ConfirmAction` (iki adımlı "emin misiniz?" düğmesi; geri alınması zor pasifleştirmelerde), `components/Pager`, `components/TextField` (alan bazlı hata), `api/admin.ts` (tipli yönetici uç fonksiyonları), `test/helpers.tsx` (testlerde ortak sahte API).

Davranış notları: Hata mesajları sunucunun Türkçe `detail`'idir (örn. "Bölümde aktif hizmet var"); doğrulama hataları ilgili alanın altında görünür. Kapsam dışı kaynak backend'de 404 döner, panel yalnız yöneticinin şubelerini listeler.

## Mimari
- `src/api/http.ts`: `fetch` sarmalayıcı. **Erişim belirteci bellekte**, yenileme belirteci `sessionStorage`'da ([[kararlar]] K-71). 401 gelince **tek** yenileme isteği (eşzamanlı istekler paylaşır; belirteç döndürme kuralı) ve istek bir kez tekrarlanır; yenileme reddedilirse oturum temizlenir ve `hospital:session-expired` olayı yayılır. Hatalar `ApiError` (Türkçe mesaj, `code`, `traceId`, alan bazlı `errors`); ağ hatası `status 0`.
- `src/api/schema.d.ts`: **üretilmiş** tipler (`npm run gen:api`, kaynak `docs/api/openapi.v1.json`); CI üretimi yeniden yapıp fark varsa kırılır. `src/api/endpoints.ts` tipli uç fonksiyonlarıdır.
- `src/auth/AuthContext.tsx`: oturum durumu (`loading | anonymous | authenticated`); sayfa yenilenince yenileme belirteciyle sessizce oturum açar. Rol yalnız ekranı kişiselleştirir; **yetkiyi backend uygular**.
- `src/lib/draft.ts`: sihirbaz seçimleri `sessionStorage` taslağındadır (giriş/kayda gidip dönünce kaybolmaz); üst seçim değişince alttakiler silinir.
- `src/lib/useAsync.ts`: değişen girdide önceki isteği iptal eder (yarış durumunda eski yanıt ekranı ezmez).
- Tarih `yyyy-MM-dd` yerel tarihten üretilir (`toISOString` UTC'ye kaydırır, kullanılmaz); randevu penceresi bugünden 60 gün; saatler şubenin saat diliminde gösterilir (`localStart`).

## 3B sahne ve erişilebilirlik ([[kararlar]] K-72, K-73)
- `three/HeroScene.tsx`: iki sarmal şerit (instanced mesh), kaydırma ilerlemesiyle döner/renk değiştirir/yaklaşır. **Metinler normal HTML**'dir (`<section>`, tek `h1`); tuval `aria-hidden`, sticky konumda, sayfa kaydırması yerel (kaydırma çubuğu, klavye, ekran okuyucu bozulmaz).
- **Sade mod:** 3B sahne çizilmez, aynı içerik düz sayfa olur. Otomatik açılır: `prefers-reduced-motion`, ≤2 çekirdek / ≤2 GB bellek veya WebGL yok; üst çubuktaki düğmeyle elle açılıp kapanır (`localStorage`). Sahne çökerse `ErrorBoundary` düz sayfaya düşer.
- Three.js yalnız ana sayfada, tembel (`React.lazy`) yüklenir; diğer ekranlar ana pakette (~90 KB gzip) kalır.
- Atla bağlantısı, `aria-live`/`role=alert` hata ve durum mesajları, odak halkası, karanlık/açık tema, mobilde sarmalanan üst çubuk.

## Çalıştırma ve test
- `docker compose up -d` → `dotnet run --project src/Presentation/Hospital.WebAPI` → `cd web && npm run dev`. Vite `/api`'yi `http://localhost:5284`'e yönlendirir (hedef `API_PROXY_TARGET`); üretimde `VITE_API_URL` verilir ve arayüz origin'i API'nin `Cors:AllowedOrigins` listesine eklenir ([[operasyon]]).
- `npm test` (Vitest + Testing Library, **50 test**): rol × rota yetkilendirme matrisi (`RouteAccess.test.tsx`), yönetim paneli akışları (rol kilidi, süper/şube yöneticisi ayrımı, şube oluşturma, çalışma saatleri gövdesi, bölüm ekleme, iki adımlı pasifleştirme + sunucu mesajı, doktor oluşturma + alan hataları, hizmet talebi onayı, nedenli iptal); doktor paneli akışları (rol kilidi, onay + liste yenileme, nedensiz red engeli, 409 mesajı, başlamamış randevuyu tamamlama kapalı, hizmet talebi, uygunluk ekleme); http istemcisi (başlıklar, sorgu kodlama, hata çevirisi, 401→yenile→tekrar, eşzamanlı 401 tek yenileme, yenileme reddi), tarih/taslak yardımcıları, uygulama akışları (sade ana sayfa, korumalı rota yönlendirmesi, hatalı giriş, başarılı giriş, sihirbaz). Bkz. [[test-stratejisi]].
- Elle doğrulama (2026-10-26): kayıt → giriş → seçimler korunarak sihirbaz → randevu talebi → "Randevularım" listesi; gerçek API'ye karşı.

## Bilinçli sınırlar / sonraki dilim
Yönetim panelinde yönetici hesabı oluşturma (`/api/admins`), doktora doğrudan hizmet atama (`PUT /api/doctors/{id}/services`) ve parola yöneticisi tarafından sıfırlama ekranı yok (kullanıcı kendi "Şifremi unuttum" akışını kullanır); yönetici/personel için e-posta doğrulama ekranı yok; personel (STAFF) rolünün paneli yok. Doktor panelinde uygunluk penceresi düzenleme (PUT) yok, yalnız ekle/sil.  Three.js paketi büyüktür (~240 KB gzip, tembel); ileride yalnız gereken modüllerle küçültülebilir.

## Üretim imajı
`web/Dockerfile` (Node ile derle → nginx ile sun), `docker-compose.app.yml`'de `web` servisi (`http://localhost:8081`): statik dosyalar + `/api/` ters vekili (tek origin, CORS gerekmez), SPA yönlendirmesi, önbellek ve güvenlik başlıkları. Ayrıntı ve vekil arkasında istemci IP'si: [[operasyon]] "Web imajı", [[kararlar]] K-82.
