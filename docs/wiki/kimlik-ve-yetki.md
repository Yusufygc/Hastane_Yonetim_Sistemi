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
- `register` yalnız **hasta** açar; doktor/personel/yönetici hesabını yalnız yönetici oluşturur (Faz 3: `POST /api/doctors|staff|admins`). Başlangıç parolasını yönetici verir (e-posta davetiyesi Faz 5'e kadar yok); kullanıcı `POST /api/auth/change-password` ile değiştirir. Parola değişince güvenlik damgası döner (eldeki erişim belirteçleri anında düşer) ve **tüm yenileme belirteçleri iptal edilir** → yeniden giriş gerekir. Hesap kuralları (e-posta, parola ≥ 8 + harf + rakam) `Application/Validation/AccountRules` içinde ortaktır.
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

## Çalışan hesapları ve kapsam (Faz 3)
- **Doktor/personel kapsamı şubeden türer:** doktorun şubesi bölümü üzerinden, personelinki doğrudan `hospital_id` ile bulunur; yönetici kapsamında değilse **404**. Yönetici hesapları ve hasta hesapları üzerinde yalnız `SuperAdmin` işlem yapar (yönetici için aynı organizasyon şartı).
- `POST /api/admins`: yalnız `SuperAdmin` (şube yöneticisi 403). `BRANCH_MANAGER` için şube zorunlu ve kapsamda olmalı; `SUPER_ADMIN` şubeye bağlanamaz. `GET /api/admins`: süper yönetici organizasyonun tümünü, şube yöneticisi yalnız kendi şubesinin yöneticilerini görür.
- `POST /api/users/{id}/deactivate|activate`: pasifleştirme güvenlik damgasını döndürür ve yenileme belirteçlerini iptal eder → doktorun oturumu **anında** düşer. Kullanıcı kendini pasifleştiremez (409).
- **Doktor kendi kaynağında:** kimlik her zaman belirteçten (`/api/doctors/me/**`); başka doktorun uygunluk/izin kaydı kimliğiyle gelinse bile doktorun kendi koleksiyonunda aranır → **404**. Doktor yalnız uzmanlık ve telefonunu değiştirir; ad, diploma, bölüm yöneticiye aittir.
- Katalog doktor listesi anonimdir ve **kişisel alan içermez** (e-posta, telefon, diploma yok) ([[kararlar]] K-45).

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
