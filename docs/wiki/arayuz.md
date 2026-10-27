# Arayüz (Faz 7)

`web/` klasörü: React 19 + TypeScript + Vite; 3B için three.js (React Three Fiber). Backend sözleşmesi [[operasyon]] ("API sözleşmesi") dosyasından türetilir. Karar gerekçeleri: [[kararlar]] K-71…K-75. Yetki modeli: [[kimlik-ve-yetki]]. Uç noktalar: [[api-uc-noktalari]].

## İlk dilim (tamamlandı)
Tanıtım sayfası, kayıt/giriş, randevu sihirbazı, randevularım. **İkinci dilim: doktor paneli** (aşağıda). Yönetici paneli sonraki dilimde ([[faz-plani]]).

| Rota | Ekran | Erişim |
|---|---|---|
| `/` | Kaydırdıkça değişen 3B tanıtım (4 bölüm) | anonim |
| `/giris`, `/kayit` | Giriş / hasta kaydı (alan bazlı hata gösterimi) | anonim |
| `/randevu-al` | Sihirbaz: şube → hizmet → doktor → tarih → saat → özet/talep | katalog anonim; talep göndermek giriş ister |
| `/randevularim` | Hasta randevuları: durum filtresi, sayfalama (`X-Total-Count`), iptal nedeniyle iptal | giriş gerekir (yalnız görsel kilit) |

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
- `npm test` (Vitest + Testing Library, **30 test**): doktor paneli akışları (rol kilidi, onay + liste yenileme, nedensiz red engeli, 409 mesajı, başlamamış randevuyu tamamlama kapalı, hizmet talebi, uygunluk ekleme); http istemcisi (başlıklar, sorgu kodlama, hata çevirisi, 401→yenile→tekrar, eşzamanlı 401 tek yenileme, yenileme reddi), tarih/taslak yardımcıları, uygulama akışları (sade ana sayfa, korumalı rota yönlendirmesi, hatalı giriş, başarılı giriş, sihirbaz). Bkz. [[test-stratejisi]].
- Elle doğrulama (2026-10-26): kayıt → giriş → seçimler korunarak sihirbaz → randevu talebi → "Randevularım" listesi; gerçek API'ye karşı.

## Bilinçli sınırlar / sonraki dilim
Yönetici paneli (şube/bölüm/hizmet, doktor/personel, hizmet talebi onayı) ve e-posta doğrulama ekranı yok; doktor panelinde uygunluk penceresi düzenleme (PUT) yok, yalnız ekle/sil. Üretim için web sunucusu imajı (statik dosya + ters vekil) henüz yok; `npm run build` `web/dist` üretir. Three.js paketi büyüktür (~240 KB gzip, tembel); ileride yalnız gereken modüllerle küçültülebilir.
