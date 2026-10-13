# Hata Yönetimi ve Doğrulama

Tüm hatalar tek biçimde (RFC 9457/7807 **ProblemDetails**), Türkçe ve makine kodlu döner. Amaç: "neyden hata aldım?" sorusu her zaman yanıt gövdesinde cevaplanır. Mimari: [[mimari]]. Kimlik: [[kimlik-ve-yetki]]. Örnekler: [[api-ornekleri]]. Karar: [[kararlar]] K-39.

## Yanıt biçimi
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "İstek gövdesi geçersiz",
  "status": 400,
  "code": "request.invalid_body",
  "traceId": "00-...",
  "errors": { "birthDate": ["Geçerli bir tarih olmalı. Biçim: yyyy-MM-dd (ör. 1999-11-11)."] }
}
```
- `code`: makine tarafından okunur (istemci buna göre davranır); `title`/`detail`/`errors`: insan için Türkçe açıklama; `traceId`: sunucu logu ile eşleştirme.
- `errors` yalnız alan bazlı hatalarda bulunur; **anahtarlar JSON alan adlarıyla aynıdır (camelCase)**: `birthDate`, `hours[0].closeTime`. Gövde düzeyi hata anahtarı `body`.

## Hata türleri ve kodları
| Durum | HTTP | `code` | Ne zaman |
|---|---|---|---|
| Alan türü/biçimi bozuk | 400 | `request.invalid_body` | `"birthDate":"11.11.1999"`, telefon sayı gönderildi, GUID bozuk… (gövde okunurken, doğrulamadan ÖNCE) |
| JSON sözdizimi | 400 | `request.invalid_json` | eksik tırnak/virgül/parantez; satır ve konum bildirilir |
| Gövde yok | 400 | `request.body_required` | POST/PUT'ta gövde unutuldu |
| Geçersiz/eksik parametre | 400 | `request.invalid_parameter` | `?hospitalId=abc`, zorunlu sorgu parametresi yok |
| İş kuralı/alan doğrulaması | 400 | `validation.failed` | FluentValidation: parola, T.C., süre, ücret… (**tüm alanlar tek yanıtta**) |
| Giriş gerekli / token geçersiz | 401 | `auth.unauthorized` / `auth.invalid_credentials` / `auth.invalid_refresh_token` | `detail` nedeni söyler (token yok, süresi dolmuş, imza geçersiz, issuer/audience) |
| Rol yetersiz | 403 | `auth.forbidden` | |
| Yok / kapsam dışı | 404 | `*.not_found` / `request.not_found` | kapsam dışı kaynağın varlığı sızdırılmaz |
| HTTP metodu yanlış | 405 | `request.method_not_allowed` | |
| Çakışma (aynı isim, eşzamanlı yarış) | 409 | `*.name_taken` / `data.conflict` | |
| İçerik türü JSON değil | 415 | `request.unsupported_media_type` | `Content-Type: application/json` zorunlu |
| Domain kuralı ihlali | 422 | `domain.rule_violation` | `detail` kuralı açıklar |
| Hız sınırı | 429 | `rate_limit.exceeded` | `Retry-After` başlığı + "N saniye sonra tekrar deneyin" |
| Beklenmeyen hata | 500 | `server.error` | genel mesaj + `traceId`; ayrıntı yalnız log'da |

## Bileşenler (nerede ne var)
- **`Application/Behaviors/ValidationBehavior`**: tüm Command/Query'lerde FluentValidation; hatalar `Error.Validation` + alan bazlı sözlük; anahtarlar `FieldNaming` ile camelCase'e çevrilir.
- **`WebAPI/Common/RequestErrorTranslator`**: ASP.NET Core'un gövde/parametre bağlama hatalarını (`BadHttpRequestException`/`JsonException`) alan bazlı Türkçe hataya çevirir. **Not:** System.Text.Json dış mesajında alanın değil kapsayan sınıfın adını yazar; alanın gerçek türü *iç istisnadan* (FormatException/InvalidOperationException) çıkarılır. İlk hatada durur (bir istekte tek tür hatası döner; düzeltince sıradaki gelir). Enum alanlarında iç istisna yoktur → genel "izin verilen değerlerden biri" mesajı.
- **`WebAPI/Common/GlobalExceptionHandler`**: tüm istisnaları yukarıdaki tabloya eşler; Development'ta 500 yanıtına `exceptionType/exceptionMessage` eklenir, **üretimde eklenmez** (yığın izi hiçbir ortamda yanıta girmez).
- **`WebAPI/Common/ProblemDetailsCustomizer`** + `UseStatusCodePages`: gövdesiz kalan 401/403/404/405/415/429 yanıtlarını Türkçe başlık, `code`, `traceId` ile doldurur; 401 için JWT'nin `WWW-Authenticate` nedenini ipucuna çevirir.
- **`WebAPI/Common/JsonContentTypeMiddleware`**: gövdeli isteklerde `Content-Type` JSON değilse yetkilendirmeden önce **415** döner. (Yoksa ASP.NET'in yapay 415 uç noktasında `AllowAnonymous` olmadığı için anonim istemci yanlış içerik türünde "401 giriş gerekir" görürdü.)
- **`RouteHandlerOptions.ThrowOnBadRequest = true`**: varsayılan yalnızca Development'ta açıktır; üretimde kapalı olsa gövde hataları gövdesiz 400 olurdu. Her ortamda açık.
- **429**: `RateLimiter.OnRejected` ProblemDetails + `Retry-After`.

## Yeni hata eklerken
1. Beklenen iş hatası → ilgili feature'da `Error.<Tür>("feature.kod", "Türkçe mesaj")` (Result ile döner, `ToProblem()` HTTP'ye çevirir).
2. Alan doğrulaması → FluentValidation validator'ı (mesajlar Türkçe, alan başına ayrı).
3. İhlal edilmemesi gereken domain kuralı → `DomainException` (422).
4. Yeni makine kodu gerekirse `ProblemCodes` (HTTP katmanı) veya feature'ın `*Errors` sınıfı.

## Bilinen sınırlar
- Aynı istekte birden fazla **tür** hatası (ör. hem tarih biçimi hem telefon sayı) tek seferde değil sırayla bildirilir (System.Text.Json ilk hatada durur). Değer/kural hataları (FluentValidation) ise hep birlikte döner.
- Sayı gönderilen tarih alanı için System.Text.Json hedef türü bildirmez; mesaj "Metin olmalı" der.

## Test
`RequestErrorTests` (HTTP, Development + Production ortamı), `RequestErrorTranslatorTests` (saf çevirici), `ValidationBehaviorTests` ([[test-stratejisi]]).
