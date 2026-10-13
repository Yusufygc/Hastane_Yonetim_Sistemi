# Test Stratejisi

Kaynak: `plan.md` §11. Faz bağlantısı: [[faz-plani]].

| Katman | Araç | Kapsam |
|---|---|---|
| Unit | xUnit, AwesomeAssertions | State geçişleri, slot üretimi (pencere × çalışma saati × izin), snapshot, iptal süresi, doktor-hizmet onayı ([[randevu-kurallari]]) |
| Entegrasyon | Gerçek PostgreSQL (compose / CI service; geçici DB) | Exclusion constraint, EF eşlemeleri, outbox |
| Eşzamanlılık | Gerçek PostgreSQL | N paralel istek aynı slota → **tam 1 başarılı, N-1 `409`** |
| Mimari | NetArchTest | Katman/feature kuralları ([[mimari]]) |
| API / Yetki | `WebApplicationFactory` | Endpoint tarama, rol×uç matrisi, IDOR, JWT doğrulama (bozuk/sahte/süresi dolmuş token), refresh rotation ([[kimlik-ve-yetki]]) |

## Zorunlu çakışma senaryoları
Birebir aynı aralık; kısmi kesişim (baş/son/içinde/kapsayan); bitişik aralıklar (çakışmaz); farklı doktor aynı saat (çakışmaz); iptal/red sonrası slot yeniden alınabilir; aynı hasta iki doktorda aynı saat; DST geçiş günü; gün sonu (23:xx); uygunluk dışı / izin günü / hastane kapalı saat; `PENDING` doktor-hizmetiyle randevu talebi (reddedilmeli).

## Çalıştırma
- Hızlı döngü: `dotnet test Hospital.sln` (Windows'ta Smart App Control kendi DLL'inizi bazen engeller — bkz. [[docker-ortam]]).
- Güvenilir yol: `./scripts/test-docker.ps1` — derleme + testler Linux .NET 9 SDK container'ında; PostgreSQL için `host.docker.internal:5432`.
- JWT geçişi sonrası: **104 birim, 9 mimari, 113 entegrasyon** (JWT: imza/süre/issuer/audience/stamp reddi, refresh rotation + reuse detection, logout, CSRF yok). Faz 2 sonu (cookie dönemi): 81 birim, 9 mimari (DI kuralı dahil, [[di-kurallari]]), 91 entegrasyon testi (kayıt, giriş, kilitleme, CSRF, oturum iptali, rol 403, hız sınırı, TPT; çakışma kısıtları ve 20 paralel istek — [[randevu-kurallari]]; şube/bölüm/hizmet CRUD, çalışma saati eşitleme, **şube yöneticisi kapsam ihlalleri → 404**, anonim katalog, organizasyon).
- Test yardımcıları: `AdminSessions` (seed'li süper yönetici girişi, DB'ye şube yöneticisi yazma), `TestAdmin` (yalnız test değerleri), `ApiClient.PutAsync/SendAsync/LoginAsync`.

Test-only uçlar: `HospitalApiFactory(includeProbeEndpoints: true)` ile `/api/test/*` uçları (rol politikaları, randevu kısıt ihlali → 409) kaydedilir; gerçek uygulamada bulunmaz, endpoint-yetki taramasına girmez.

## Ortam
Entegrasyon testleri `docker compose up -d` ile çalışan PostgreSQL gerektirir ([[docker-ortam]]). Testcontainers **kullanılmıyor**: Smart App Control imzasız DLL'i engelliyor ([[kararlar]] K-20).
