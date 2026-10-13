# Kimlik Doğrulama ve Yetkilendirme

Kaynak: `plan.md` §9. Endpoint'ler: [[api-uc-noktalari]]. Testler: [[test-stratejisi]]. Örnek istekler: [[api-ornekleri]]. Karar: [[kararlar]] K-38 (cookie → JWT).

## JWT Bearer kimlik doğrulama
- **Erişim belirteci (accessToken):** HS256 imzalı, kısa ömürlü (varsayılan **15 dk**, `Jwt:AccessTokenMinutes`) JWT. İstemci her istekte `Authorization: Bearer <accessToken>` gönderir. Cookie ve CSRF **yoktur**.
- **Claim'ler:** `sub`, `email`, `role` (PATIENT/DOCTOR/STAFF/ADMIN), `security_stamp`, `jti`; yöneticide `organization_id`, `access_level` (`SUPER_ADMIN`/`BRANCH_MANAGER`), şube yöneticisinde `hospital_id`. Claim adları olduğu gibi kalır (`MapInboundClaims=false`). **Rol token'dan gelir, URL'de rol öneki yok.**
- **Yenileme belirteci (refreshToken):** opak, 256 bit rastgele, varsayılan **7 gün** (`Jwt:RefreshTokenDays`). Veritabanında yalnız **SHA-256 özeti** tutulur (`refresh_token` tablosu); düz değer yalnız girişte/yenilemede istemciye verilir.
- **Döndürme (rotation) + yeniden kullanım tespiti:** `POST /api/auth/refresh` eski belirteci iptal eder, yeni çift verir. İptal edilmiş bir belirteç tekrar sunulursa çalınma şüphesiyle kullanıcının **tüm etkin yenileme belirteçleri iptal edilir**.
- **Çıkış:** `POST /api/auth/logout` (Bearer + gövdede refreshToken) yenileme belirtecini iptal eder; idempotent, başkasının belirtecini iptal edemez (sessizce yok sayılır). Erişim JWT'si doğal süresi dolana kadar teknik olarak geçerli kalır (kısa ömürlü).
- **Anında iptal (JWT'nin zayıf noktası kapatıldı):** `OnTokenValidated` her istekte `IAccountSessionReader` ile hesabın aktifliğini ve `security_stamp` eşleşmesini DB'den doğrular → pasife alınan veya parolası değişen kullanıcının henüz süresi dolmamış erişim belirteci de **anında** geçersiz olur. Bedel: her istekte tek basit PK sorgusu.
- İmza anahtarı `Jwt:SigningKey` (≥ 32 bayt) **koda/depoya konmaz**; geliştirme değeri `appsettings.Development.json`'da, üretimde `Jwt__SigningKey` ortam değişkeni/gizli depo. Eksik/kısa ise uygulama başlarken hata verir (`JwtSettings.Validated`). Issuer/Audience doğrulanır, algoritma yalnız HS256, saat farkı 30 sn.
- `register` yalnız **hasta** açar; doktor/personel/yönetici hesabını yalnız yönetici oluşturur (Faz 3).
- Giriş hataları tek tip: yanlış parola, bilinmeyen e-posta, pasif ve kilitli hesap aynı `401 auth.invalid_credentials` döner (hesap varlığı sızmaz; bilinmeyen e-postada da parola özeti hesaplanır). Kilit: 5 hatalı denemede 15 dk (`Auth:*`).
- Hız sınırı: `register`/`login`/`refresh` IP başına dakikada 10 (`RateLimiting:Auth:*`) → 429.
- Parola özeti: ASP.NET Identity PBKDF2. TC numarası şimdilik düz saklanır (şifreleme Faz 6 KVKK maddesi).

## Neden CSRF yok?
CSRF, tarayıcının **cookie'yi otomatik** göndermesini kötüye kullanır. Token `Authorization` başlığına istemci kodu tarafından elle konduğu için tarayıcı kendiliğinden göndermez; saldırgan site senin adına istek atamaz. Bu yüzden antiforgery/CSRF katmanı kaldırıldı ([[kararlar]] K-38). CORS yalnız bilinen origin'lere açıktır ve `AllowCredentials` gerekmez.
Bedel/dikkat: token'ı istemci saklar (XSS saldırısında çalınabilir) — arayüzde kullanıcıdan gelen içerik kaçışlanmalı, token `localStorage` yerine bellekte tutulması tercih edilir.

## Yönetici kapsamı (Faz 2)
- Token claim'leri: `organization_id`, `access_level`, şube yöneticisinde `hospital_id`. `AdminScope.From(ICurrentUser)` bunlardan kapsamı üretir; yönetici değilse veya claim eksik/tutarsızsa `null` → handler `Forbidden` döner.
- **Her handler kapsamı uygular** (`HospitalErrors.CheckAccess`): kapsam dışı şube, bölüm veya hizmet → **404** (varlığı sızdırılmaz). Okuma sorguları (`IHospitalReader`, `IServiceReader`) kapsam filtresini SQL'de uygular.
- Yeni şube eklemek ve `/api/organization` yalnız `SuperAdmin` politikası (`ADMIN` + `access_level=SUPER_ADMIN`); şube yöneticisi 403 alır.
- İlk süper yönetici: `Database:SeedOnStartup` açıkken `DatabaseSeeder`, `Seed:SuperAdmin:Email/Password` yapılandırmasından oluşturur (hiç yönetici yoksa). Geliştirme değerleri `appsettings.Development.json`'da; **üretimde ortam değişkeni** (`Seed__SuperAdmin__Email`, `Seed__SuperAdmin__Password`).

## Yetkilendirme (4 savunma katmanı)
Politikalar: `Policies.Patient`, `Doctor`, `Admin`, `SuperAdmin`, `Authenticated`.
1. **Fallback policy = giriş zorunlu**; anonim uçlar açıkça `AllowAnonymous` (`login`, `register`, `refresh`, `catalog/*`). Bilinmeyen rotalar da anonim kullanıcıya 401 döner (rota varlığı sızmaz).
2. **Uca özel rol politikası:** `/api/doctors/me/**` → `Policies.Doctor`; hasta → 403.
3. **Kaynak sahipliği (handler, `ICurrentUser`):** doktor yalnız kendi randevusunu onaylar; hasta yalnız kendininkini görür. Başkasının kaynağı → 404.
4. **Kapsam filtresi:** `hospital_id`/`organization_id` token'dan okunur, ilgili sorgulara zorunlu uygulanır.

| Durum | Yanıt |
|---|---|
| Token yok / bozuk / imzası geçersiz / süresi dolmuş / güvenlik damgası uyuşmuyor | 401 |
| Rol yetersiz | 403 |
| Rol uygun, kaynak başkasının | 404 |
| Kapsam dışı şube | 404 |
| Çakışan slot | 409 |

Arayüzdeki sayfa kilitleri yalnız görsel; koruma backend'de. Yetki testleri: endpoint tarama + rol×uç matrisi + IDOR + JWT doğrulama (bozuk/sahte/süresi dolmuş/yanlış issuer-audience/anahtar) ([[test-stratejisi]]). Mimari: [[mimari]].

## OpenAPI / Scalar
Doküman Bearer şeması içerir; Scalar'da **Authorize** alanına `accessToken` yapıştırılır. Yetki gerektiren her uçta gerekli politika açıklamada yazar ([[api-ornekleri]]).
