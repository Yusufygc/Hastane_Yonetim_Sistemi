# Kimlik Doğrulama ve Yetkilendirme

Kaynak: `plan.md` §9. Endpoint'ler: [[api-uc-noktalari]]. Testler: [[test-stratejisi]].

## Cookie tabanlı kimlik doğrulama
- ASP.NET Core Cookie Authentication; bilet Data Protection ile şifreli; `HttpOnly`, `Secure`, `SameSite=Lax/Strict`.
- Claim'ler: `sub`, `role`, `hospital_id`, `organization_id`, `access_level`. **Rol cookie'den gelir, URL'de rol öneki yok.**
- Kayan süre; `POST /api/auth/logout` cookie'yi siler.
- `OnValidatePrincipal` her istekte aktiflik ve `security_stamp` doğrular → pasife alınan/rolü değişen kullanıcının oturumu düşer.
- API yönlendirme yapmaz: `OnRedirectToLogin` → 401, `OnRedirectToAccessDenied` → 403.
- `register` yalnız **hasta** açar; doktor/personel/yönetici hesabını yalnız yönetici oluşturur.
- Giriş: rate limiting + kilitleme (`failed_login_count`, `lockout_end`).

## Cookie'ye özgü önlemler
- **CSRF:** `SameSite` + antiforgery token (`X-XSRF-TOKEN`), `GET /api/auth/csrf`.
- **CORS:** ayrı origin için `AllowCredentials` + açık origin listesi, joker yok. Üretimde tek origin (reverse proxy) önerilir.
- Sırlar `user-secrets`/ortam değişkeni; Data Protection anahtarları çoklu örnekte paylaşılan depoda.

## Uygulama notları (Faz 1)
- Cookie adı `hospital.auth`, antiforgery cookie `hospital.csrf`, başlık `X-XSRF-TOKEN`. `GET /api/auth/csrf` belirteç verir; **kimlik değişince (giriş/çıkış) belirteç yeniden alınmalı** (belirteç kimliğe bağlı). `/api` altındaki POST/PUT/PATCH/DELETE hepsi (login/register dahil) belirteç ister; eksikse 400 `csrf.invalid`.
- Giriş hataları tek tip: yanlış parola, bilinmeyen e-posta, pasif ve kilitli hesap aynı `401 auth.invalid_credentials` döner (hesap varlığı sızmaz). Bilinmeyen e-postada da parola özeti hesaplanır (zamanlama eşitleme).
- Kilit: 5 hatalı denemede 15 dk (`Auth:MaxFailedLoginAttempts`, `Auth:LockoutMinutes`).
- Hız sınırı: `auth` politikası IP başına dakikada 10 (`RateLimiting:Auth:*`) → 429. Ters vekil arkasında `ForwardedHeaders` gerekir (Faz 6).
- Oturum doğrulama: `OnValidatePrincipal` → `IAccountSessionReader` (her istekte `is_active` + `security_stamp` sorgusu). Parola özeti: ASP.NET Identity PBKDF2.
- Kayıt: `register` yalnız `Patient` açar. TC numarası şimdilik düz saklanır (şifreleme/hash Faz 6 KVKK maddesi).
- Rol politikaları testi için yalnız test projesinde kayıtlı sahte uçlar kullanılır (`/api/test/*`); gerçek Doctor/Admin hesapları Faz 3'te.

## Yetkilendirme (4 savunma katmanı)
Politikalar: `Policies.Patient`, `Doctor`, `Admin`, `SuperAdmin`, `Authenticated`.
1. **Fallback policy = giriş zorunlu**; anonim uçlar açıkça `AllowAnonymous` (`login`, `register`, `catalog/*`).
2. **Uca özel rol politikası:** `/api/doctors/me/**` → `Policies.Doctor`; hasta → 403.
3. **Kaynak sahipliği (handler, `ICurrentUser`):** doktor yalnız kendi randevusunu onaylar; hasta yalnız kendininkini görür. Başkasının kaynağı → 404.
4. **Kapsam filtresi:** `hospital_id`/`organization_id` claim'den okunur, ilgili sorgulara zorunlu uygulanır.

| Durum | Yanıt |
|---|---|
| Cookie yok/süresi dolmuş | 401 |
| Rol yetersiz | 403 |
| Rol uygun, kaynak başkasının | 404 |
| Kapsam dışı şube | 404 |
| Çakışan slot | 409 |

Arayüzdeki sayfa kilitleri yalnız görsel; koruma backend'de. Yetki testleri: endpoint tarama + rol×uç matrisi + IDOR ([[test-stratejisi]]). Karar geçmişi: [[kararlar]]. Mimari: [[mimari]].
