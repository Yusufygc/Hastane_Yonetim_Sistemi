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
