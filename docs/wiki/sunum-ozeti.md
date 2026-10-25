# Sunum Özeti (Backend)

Ödev sunumu / savunma için tek sayfalık mimari özet. Ayrıntı için ilgili sayfalara gidin. Gereksinim eşlemesi: [[odev-gereksinimleri]]. Karar gerekçeleri: [[kararlar]].

## Sistem tek cümlede
Çok şubeli (veya tek şubeli) hastane/klinik için, hastanın doktor-hizmet seçip **çakışmasız** randevu talep ettiği, doktorun onayladığı, e-posta bildirimlerinin güvenilir gittiği REST API. .NET 9, PostgreSQL, JWT Bearer.

## Mimari (bkz. [[mimari]], [[di-kurallari]])
- **Onion Architecture**, 5 proje: `Domain` ← `Application` ← (`Infrastructure`, `Persistence`) ← `WebAPI`. Bağımlılık yalnız içeriye; Infrastructure ile Persistence birbirini bilmez. `NetArchTest` ile **mimari testlerle** zorlanır.
- **Feature klasörleri:** her katmanda `Features/<Ad>/`; Command/Query/Handler/Validator yan yana.
- **CQRS-lite:** Mediator (source generator) + FluentValidation pipeline; sonuç `Result<T>`/`Error` (beklenen hata istisna değil).
- **Minimal API** + ProblemDetails hata sözleşmesi ([[hata-yonetimi]]).

## Öne çıkan tasarım kararları
| Konu | Çözüm | Sayfa |
|---|---|---|
| Çift rezervasyon | Önce uygulama kontrolü, sonra **DB exclusion constraint** (`btree_gist`); 20 paralel istek → 1×201, 19×409 | [[randevu-kurallari]] |
| Randevu yaşam döngüsü | **State Pattern** (PENDING→APPROVED/REJECTED/CANCELLED/COMPLETED) + `xmin` iyimser eşzamanlılık | [[tasarim-kaliplari]] |
| Slot üretimi | Saf fonksiyon `SlotCalculator`, DST'ye duyarlı, I/O yok → kolay test | [[randevu-kurallari]] |
| Bildirim | **Transactional Outbox** (olay SaveChanges içinde, `FOR UPDATE SKIP LOCKED`, üstel geri çekilme, `dedup_key`) | [[bildirimler]] |
| Kimlik | JWT Bearer (15 dk) + döndürülen refresh token, her istekte `security_stamp` kontrolü | [[kimlik-ve-yetki]] |
| Yetki | Politikalar (Patient/Doctor/Admin/SuperAdmin), şube kapsamı (kapsam dışı → 404), kaynak sahipliği handler'da | [[kimlik-ve-yetki]] |
| KVKK | T.C. kimlik no **AES-256-GCM** ile şifreli, arama için HMAC **kör indeks**, yanıtta maskeli | [[veritabani]], [[operasyon]] |
| Sözleşme | OpenAPI **dondurulmuş** (`docs/api/openapi.v1.json`), tipli yanıtlar, sözleşme testi | [[operasyon]] |
| İzleme | `X-Correlation-Id` = `traceId` = log kapsamı | [[operasyon]] |
| Dayanıklılık | Bekleyen talep zaman aşımı arka plan işi, SMTP kapalıyken randevu akışı bozulmaz | [[operasyon]], [[bildirimler]] |

## Kalite güvencesi (bkz. [[test-stratejisi]])
- Birim (domain/validator/koruyucu/SlotCalculator) + mimari + entegrasyon (gerçek PostgreSQL, gerçek SMTP/Mailpit).
- **Yetki matrisi:** 67 uç × 6 kimlik; **IDOR matrisi:** başkasının kaynağı 404.
- **Sözleşme testi:** üretilen OpenAPI dosyadan farklıysa test kırılır.
- **Plan testi:** kritik sorgular indeks kullanır (`EXPLAIN`).
- Testler container'da (`scripts/test-docker.ps1`) ve CI'da çalışır.

## Demo akışı (5 dk)
1. `docker compose up -d` → `dotnet run` (veya `docker-compose.app.yml` ile hepsi container'da) → Scalar `/scalar/v1`.
2. Hasta kaydı → giriş → anonim slot sorgusu → randevu talebi → Mailpit'te doktora e-posta.
3. Doktor girişi → onay → hastaya e-posta; aynı slota ikinci talep → `409 appointment.slot_taken`.
4. Başka hastanın randevusuna erişim → `404`; hasta rolüyle yönetici ucu → `403`.
5. Yanıt başlığında `X-Correlation-Id`; hata gövdesindeki `traceId` ile aynı.

## Bilinçli sınırlar
Anahtar döndürme, denetim izi, kullanıcı bazlı hız sınırı yok; sayfalama sayfa numarası tabanlıdır ([[operasyon]] "Bilinçli sınırlar"). Arayüz Faz 7'de ([[faz-plani]]).
