# Hastane / Klinik Randevu Sistemi

[![CI](https://github.com/Yusufygc/Hastane_Yonetim_Sistemi/actions/workflows/ci.yml/badge.svg)](https://github.com/Yusufygc/Hastane_Yonetim_Sistemi/actions/workflows/ci.yml)
![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)
![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16-336791)
![React 19](https://img.shields.io/badge/React-19-61DAFB)
![Onion Architecture](https://img.shields.io/badge/mimari-Onion%20%2B%20feature%20klas%C3%B6rl%C3%BC-0f766e)

Çok şubeli (veya tek şubeli) bir hastane/klinik için **çakışmasız randevu yönetim sistemi**. Hasta şube, hizmet, doktor ve saat seçip randevu talep eder; doktor onaylar veya reddeder; yönetici kurumu, doktorları ve hizmetleri yönetir; e-posta bildirimleri sistem arızalarında bile kaybolmadan gider.

Arka uç **.NET 9 + PostgreSQL** (Onion Architecture, feature klasörleri, Minimal API), ön yüz **React 19 + TypeScript + Vite** ve kaydırdıkça değişen bir **3B tanıtım sahnesidir**. BSM515 dersi dönem projesi olarak geliştirilmiştir (Seçenek A: Hastane/Klinik Randevu Sistemi).

![Ana sayfa: kaydırmayla değişen 3B tanıtım sahnesi](docs/img/ana-sayfa.png)

## İçindekiler
- [Öne çıkanlar](#öne-çıkanlar)
- [Ekran görüntüleri](#ekran-görüntüleri)
- [Hızlı başlangıç](#hızlı-başlangıç)
- [Mimari](#mimari)
- [Randevu kuralları](#randevu-kuralları)
- [Veri modeli](#veri-modeli)
- [API](#api)
- [Kimlik doğrulama ve yetki](#kimlik-doğrulama-ve-yetki)
- [Güvenlik ve KVKK](#güvenlik-ve-kvkk)
- [E-posta bildirimleri](#e-posta-bildirimleri)
- [Arayüz](#arayüz)
- [Test](#test)
- [Yapılandırma ve dağıtım](#yapılandırma-ve-dağıtım)
- [Depo yapısı](#depo-yapısı)
- [Teknoloji yığını](#teknoloji-yığını)
- [Belgeler](#belgeler)
- [Geliştirme akışı](#geliştirme-akışı)
- [Lisans ve atıflar](#lisans-ve-atıflar)

## Öne çıkanlar

| Konu | Çözüm |
|---|---|
| **Çift rezervasyon yok** | Önce uygulamada kontrol, sonra veritabanında **exclusion constraint** (`btree_gist`). Gerçek PostgreSQL'de 20 paralel istek: tam 1 başarı (`201`), 19 çakışma (`409`). |
| **Randevu yaşam döngüsü** | **State Pattern** (Bekleyen → Onaylı / Reddedilmiş / İptal / Tamamlanmış) ve `xmin` ile iyimser eşzamanlılık. |
| **Slot üretimi** | Saf fonksiyon `SlotCalculator`: çalışma saati × doktor uygunluğu × izin × mevcut randevular, saat dilimi ve DST'ye duyarlı, I/O yok. |
| **Güvenilir e-posta** | **Transactional Outbox**: olay, randevuyla aynı işlemde yazılır; arka plan işi `FOR UPDATE SKIP LOCKED` ile gönderir, üstel geri çekilmeyle yeniden dener. SMTP kapalıyken randevu akışı bozulmaz. |
| **Katmanlı yetki** | Politikalar (Hasta / Doktor / Yönetici / Süper yönetici), şube kapsamı (kapsam dışı kaynak `404`), kaynak sahipliği handler'da. 69 uç × 6 kimlik matris testi, IDOR testi. |
| **KVKK** | T.C. kimlik no **AES-256-GCM** ile şifreli, arama için HMAC **kör indeks**, yanıtlarda maskeli. |
| **Sözleşme güvencesi** | **Dondurulmuş OpenAPI** (`docs/api/openapi.v1.json`); üretilen belge dosyadan farklıysa test kırılır. Web tipleri bu dosyadan üretilir. |
| **İzlenebilirlik** | Her yanıtta `X-Correlation-Id`; hata gövdesindeki `traceId` ve log kapsamıyla aynı değer. |
| **Mimari testle zorlanan kurallar** | Bağımlılıklar yalnız içeriye; Infrastructure ile Persistence birbirini bilmez (**NetArchTest**). |

## Ekran görüntüleri

### Tanıtım sayfası (3B kaydırmalı sahne)

Sayfa kaydırıldıkça dört bölüm ve dört 3B model değişir: steteskop, hasta odası, mikroskop, doktor.

<table>
  <tr>
    <td width="50%"><img src="docs/img/ana-sayfa.png" alt="3B steteskop"><br><sub><b>1/4 Steteskop:</b> randevunuz saniyeler içinde.</sub></td>
    <td width="50%"><img src="docs/img/ana-sayfa-2.png" alt="3B hasta odası"><br><sub><b>2/4 Hasta odası:</b> şube, klinik ve hizmet seçimi.</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/img/ana-sayfa-3.png" alt="3B mikroskop"><br><sub><b>3/4 Mikroskop:</b> boş saatler anında görünür, çakışma olmaz.</sub></td>
    <td width="50%"><img src="docs/img/ana-sayfa-4.png" alt="3B doktor"><br><sub><b>4/4 Doktor:</b> doktor onaylar, e-posta gelir.</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/img/ana-sayfa-koyu.png" alt="Koyu tema"><br><sub><b>Koyu tema:</b> açık, koyu ve sistem teması desteklenir.</sub></td>
    <td width="50%"><img src="docs/img/mobil-randevularim.png" alt="Mobil görünüm"><br><sub><b>Mobil:</b> 390 px genişlikte uyumlu yerleşim.</sub></td>
  </tr>
</table>

### Hasta

<table>
  <tr>
    <td width="50%"><img src="docs/img/kayit.png" alt="Kayıt ol"><br><sub><b>Kayıt:</b> ad, e-posta, parola, geçerli T.C. kimlik no ve doğum tarihi.</sub></td>
    <td width="50%"><img src="docs/img/giris.png" alt="Giriş yap"><br><sub><b>Giriş:</b> JWT oturumu; "Şifremi unuttum" bağlantısı.</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/img/randevu-sihirbazi.png" alt="Randevu sihirbazı"><br><sub><b>Randevu sihirbazı:</b> şube → hizmet → doktor → tarih/saat → özet ve talep.</sub></td>
    <td width="50%"><img src="docs/img/randevularim.png" alt="Randevularım"><br><sub><b>Randevularım:</b> talep sonrası "Onay bekliyor" durumu, filtre ve iptal.</sub></td>
  </tr>
</table>

### Doktor

<table>
  <tr>
    <td width="50%"><img src="docs/img/doktor-paneli.png" alt="Doktor paneli"><br><sub><b>Randevular:</b> bekleyen talebi onaylama veya reddetme.</sub></td>
    <td width="50%"><img src="docs/img/doktor-takvim.png" alt="Doktor takvimi"><br><sub><b>Takvim:</b> haftalık görünümde onaylı randevular; tamamla veya iptal et.</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/img/doktor-hizmetlerim.png" alt="Doktor hizmetleri"><br><sub><b>Hizmetlerim:</b> hizmet talebi ve yönetici onay durumu.</sub></td>
    <td width="50%"><img src="docs/img/doktor-uygunluk.png" alt="Uygunluk ve izin"><br><sub><b>Uygunluk ve izin:</b> haftalık çalışma pencereleri ve izin günleri.</sub></td>
  </tr>
</table>

### Yönetici

<table>
  <tr>
    <td width="50%"><img src="docs/img/yonetim-paneli.png" alt="Yönetim paneli"><br><sub><b>Randevular:</b> şube kapsamındaki tüm randevular ve yönetim sekmeleri.</sub></td>
    <td width="50%"><img src="docs/img/yonetim-subeler.png" alt="Şubeler"><br><sub><b>Şubeler:</b> kurum adı, şube bilgileri, çalışma saatleri.</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/img/yonetim-bolumler.png" alt="Bölümler"><br><sub><b>Bölümler:</b> bölüm ekleme, yeniden adlandırma, pasifleştirme.</sub></td>
    <td width="50%"><img src="docs/img/yonetim-hizmetler.png" alt="Hizmetler"><br><sub><b>Hizmetler:</b> süre ve ücretli hizmet tanımı.</sub></td>
  </tr>
</table>

### API ve e-posta

<table>
  <tr>
    <td width="50%"><img src="docs/img/api-scalar.png" alt="Scalar API dokümanı"><br><sub><b>API dokümanı:</b> Scalar ile sunulan OpenAPI belgesi (<code>/scalar/v1</code>).</sub></td>
    <td width="50%"><img src="docs/img/mailpit-eposta.png" alt="Mailpit e-postası"><br><sub><b>E-posta bildirimi:</b> randevu talebinde doktora giden Türkçe e-posta (Mailpit).</sub></td>
  </tr>
</table>

Arayüz açık, koyu ve sistem temasını destekler; **sade mod** 3B sahneyi kapatır.

## Hızlı başlangıç

### Gereksinimler
- .NET 9 SDK (`global.json` 9.0.x'e sabitler)
- Docker Desktop (PostgreSQL ve Mailpit için)
- Node.js (yalnız arayüz için)

### 1. Arka uç
```bash
docker compose up -d
dotnet run --project src/Presentation/Hospital.WebAPI
```

| Adres | Ne |
|---|---|
| http://localhost:5284/health | Sağlık ucu |
| http://localhost:5284/scalar/v1 | API dokümanı (yalnız Development) |
| http://localhost:8025 | Mailpit: randevu ve parola e-postaları burada görünür |

Development'ta ilk açılışta varsayılan kurum/şube, örnek bölüm ve hizmetler ile ilk süper yönetici otomatik oluşur.

### 2. Arayüz
```bash
cd web
npm install
npm run dev        # http://localhost:5173  (/api isteklerini http://localhost:5284'e yönlendirir)
```

### 3. Giriş bilgileri (yalnız yerel geliştirme)

| Rol | Hesap | Giriş sonrası |
|---|---|---|
| Hasta | `/kayit` ile kendini kaydeder (geçerli T.C. kimlik no gerekir) | `/randevu-al` |
| Doktor | `doktor@hastane.local` / `Doktor12345x` | `/doktor` |
| Yönetici | `admin@hastane.local` / `Admin12345x` | `/yonetim` |

Bu hesaplar `appsettings.Development.json` içindeki `Seed:*` bölümünden gelir ve **üretimde açılmaz**. Üretimde ilk yönetici `Seed__SuperAdmin__Email/Password` ortam değişkenleriyle verilir. Doktor ve yönetici hesaplarını yönetici açar; hasta kendini doktor veya yönetici yapamaz.

### Her şey container'da (PostgreSQL + Mailpit + API + web)
```bash
cp .env.example .env        # gizli değerleri düzenleyin: JWT_SIGNING_KEY, DATA_PROTECTION_KEY, SEED_ADMIN_*
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```
API http://localhost:8080 adresinde çalışır (sağlık: `/health`). Web imajı nginx ile statik dosyaları sunar ve `/api/` isteklerini API'ye vekiller.

### Canlı doğrulama
```bash
bash scripts/smoke-test.sh      # kayıt → giriş → slot → randevu → onay → e-posta akışını uçtan uca dener
```

## Mimari

![Sistemin genel yapısı](docs/img/mimari-genel.png)

**Onion Architecture**, 5 proje, **feature klasörlü**: her katmanda `Features/<Ad>/` altında Command, Query, Handler ve Validator yan yana durur.

![Onion halkaları ve bağımlılık yönü](docs/img/onion.png)

- **Bağımlılık yönü dıştan içe.** `Domain` hiçbir katmana bağlı değildir. `Infrastructure` ile `Persistence` birbirini bilmez. Kural **NetArchTest** ile derleme sonrası testte zorlanır.
- **CQRS-lite:** [Mediator](https://github.com/martinothamar/Mediator) (source generator, çalışma zamanı yansıması yok) + FluentValidation pipeline'ı. Beklenen hatalar istisna değil, `Result<T>` / `Error` ile taşınır.
- **Minimal API.** Her endpoint açık bir yetki politikası veya `AllowAnonymous` taşır; endpoint envanteri testte taranır.
- **DI kuralı:** her katman kendi `Extensions/<Katman>Ext.cs` dosyasında `Add<Katman>` ile servislerini kaydeder (`AddApplication`, `AddInfrastructure`, `AddPersistence`, `AddWebApi`); `Program.cs` yalnız bunları çağırır.
- **Hata sözleşmesi:** tüm hatalar **ProblemDetails** (`application/problem+json`) + makine okunur `code`, `traceId`, alan bazlı `errors`, Türkçe `title`. İstisnalar tek `GlobalExceptionHandler` ile yönetilir.

## Randevu kuralları

![Randevu durum makinesi](docs/img/randevu-durumlari.png)

- `Pending` ve `Approved` slotu tutar; `Rejected`, `Cancelled`, `Completed` slotu serbest bırakır.
- **Slot üretimi:** hizmetin süresi sabit adım olur; slotlar şube çalışma saatleri, doktorun haftalık uygunluğu ve izinleri ile mevcut randevulara göre hesaplanır. Saatler şubenin saat diliminde gösterilir.
- **Çakışma iki katmanda engellenir:** uygulama kontrolü ve PostgreSQL exclusion constraint'leri (`ex_doctor_no_overlap`, `ex_patient_no_overlap`). Veritabanı ihlali (`23P01`) `409 appointment.slot_taken` olarak döner. Aynı hasta aynı saatte iki doktora randevu alamaz.
- **Doktor-hizmet ataması:** doktor hizmet talep eder (`PENDING`), yönetici onaylar veya reddeder. Onaysız hizmetle randevu alınamaz.
- **İptal:** hasta, onaylı randevuyu başlamasına 2 saatten az kala iptal edemez (`409 appointment.cancellation_deadline_passed`). Doktor ve kapsamdaki yönetici gerekçeyle iptal edebilir.
- **Zaman aşımı:** doktor yanıtlamayan bekleyen talepler arka plan işiyle kapatılır.
- **Anlık görüntü (snapshot):** randevu, oluşturulduğu andaki hizmet süresi ve ücretini saklar; sonradan fiyat değişse de geçmiş randevu değişmez.

Ayrıntı: [docs/wiki/randevu-kurallari.md](docs/wiki/randevu-kurallari.md)

## Veri modeli

![Veri modelinin özeti](docs/img/veri-modeli.png)

Tüm kimlikler **Guid v7** (tahmin edilemez, zamana göre sıralı). Kullanıcı hiyerarşisi **TPT** (Table-Per-Type): `USER_ACCOUNT` taban tablosunu `PATIENT`, `DOCTOR`, `STAFF` ve `ADMIN` alt tabloları paylaşır. Varlıklar kapsüllenmiştir: özelliklerin setter'ı özeldir, durum değişimi yalnız davranış metotlarıyla olur ve kurallar `DomainException` ile korunur. Ayrıntı: [docs/wiki/veritabani.md](docs/wiki/veritabani.md), [docs/wiki/domain-modeli.md](docs/wiki/domain-modeli.md).

## API

- **69 uç nokta**, rol URL'de taşınmaz; kaynak odaklı rotalar ve `me` kalıbı kullanılır (örn. `GET /api/doctors/me/appointments`).
- **Dondurulmuş sözleşme:** [docs/api/openapi.v1.json](docs/api/openapi.v1.json). Bilinçli değişiklikten sonra `./scripts/update-openapi-snapshot.ps1` ile güncellenir; `OpenApiContractTests` farkı yakalar.
- **Sayfalama:** liste uçlarında `page` / `pageSize` ve `X-Total-Count` başlığı.
- **Hız sınırlama:** kimlik uçlarında; vekil arkasında gerçek istemci IP'si yalnız güvenilen ağlardan gelen `X-Forwarded-For` ile belirlenir.
- **Örnekler:** curl ve Scalar akışı için [docs/wiki/api-ornekleri.md](docs/wiki/api-ornekleri.md); tüm uçlar için [docs/wiki/api-uc-noktalari.md](docs/wiki/api-uc-noktalari.md).

```bash
# Kayıt → giriş → erişim belirteciyle çağrı
curl -X POST http://localhost:5284/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@hastane.local","password":"Admin12345x"}'
# yanıttaki accessToken değerini Authorization: Bearer <token> başlığında kullanın
```

## Kimlik doğrulama ve yetki

- **JWT Bearer** (HS256). Kısa ömürlü erişim belirteci (15 dk) + **döndürülen (rotation) refresh token** (7 gün). Cookie ve CSRF yoktur.
- Her istekte `security_stamp` kontrolü: parola değişimi veya hesap pasifleştirme eski belirteçleri hemen geçersiz kılar.
- **Roller:** `PATIENT`, `DOCTOR`, `STAFF`, `ADMIN` (`SUPER_ADMIN` / `BRANCH_MANAGER`). Şube yöneticisi yalnız kendi şubesini görür; kapsam dışı kaynak `404` döner (varlığı sızdırmaz).
- **Parola sıfırlama:** e-postayla tek kullanımlık bağlantı. Veritabanında yalnız belirtecin SHA-256 özeti durur; yanıt, kayıtlı olsun olmasın aynıdır (hesap sayımı engellenir).
- Başarısız girişte kilitleme ve hız sınırı.

Ayrıntı: [docs/wiki/kimlik-ve-yetki.md](docs/wiki/kimlik-ve-yetki.md)

## Güvenlik ve KVKK

- T.C. kimlik no **AES-256-GCM** ile şifrelenir; aramada kullanılan **HMAC kör indeks** ile eşleşme sağlanır; API yanıtlarında maskeli döner. Şifreleme anahtarı yoksa uygulama açılmaz (**şifreleme anahtarını yedekleyin**).
- Parolalar PBKDF2 ile özetlenir. Zorunlu ayarlar (JWT anahtarı, veri koruma anahtarı) eksik veya zayıfsa açılışta açık hata verilir.
- API imajı root olmayan kullanıcıyla çalışır; web imajı CSP ve önbellek başlıkları ekler.
- Bilinçli sınırlar: anahtar döndürme, denetim izi (audit log) ve kullanıcı bazlı hız sınırı yoktur.

## E-posta bildirimleri

Randevu talebi, onay, red ve iptalinde ilgili tarafa Türkçe e-posta gider.

```
domain olayı → SaveChanges içinde EMAIL_OUTBOX satırı → BackgroundService işleyici → SMTP
```

- Olay ve e-posta kaydı **aynı veritabanı işleminde** yazılır; randevu kaydedildiyse e-posta da kuyruktadır.
- İşleyici `FOR UPDATE SKIP LOCKED` ile çalışır: birden çok işleyici aynı e-postayı iki kez göndermez.
- Başarısızlıkta üstel geri çekilme; 5 denemeden sonra `FAILED`. `dedup_key` aynı olay için tekrarı önler.
- Yerelde tüm e-postalar Mailpit'te (http://localhost:8025) görünür.

Ayrıntı: [docs/wiki/bildirimler.md](docs/wiki/bildirimler.md)

## Arayüz

`web/` klasörü: React 19 + TypeScript + Vite + React Router. Ana sayfada kaydırdıkça değişen 4 bölümlük 3B sahne **three.js / React Three Fiber** ile çizilir (tembel yüklenir, HDRI ışık ve gölge kullanır).

| Rota | Ekran | Erişim |
|---|---|---|
| `/` | Kaydırmalı 3B tanıtım | anonim |
| `/giris`, `/kayit`, `/sifremi-unuttum`, `/sifre-sifirla` | Giriş, kayıt, parola sıfırlama | anonim |
| `/randevu-al` | Sihirbaz: şube → hizmet → doktor → tarih/saat → özet | katalog anonim, talep için giriş |
| `/randevularim` | Hasta randevuları: filtre, sayfalama, iptal | hasta |
| `/doktor/*` | Randevular, takvim, hizmetlerim, uygunluk ve izin, profil | doktor |
| `/yonetim/*` | Randevular, hizmet talepleri, doktorlar, personel, şubeler, bölümler, hizmetler | yönetici |

- **Oturum:** erişim belirteci yalnız bellekte, yenileme belirteci `sessionStorage`'da; yenileme tek uçuşludur (eşzamanlı 401'ler tek istek üretir).
- **Rol kilidi görseldir:** yanlış rolle girilen sayfa korumalı uca hiç istek atmaz; atılsa bile API `403` verir.
- **Tipler** dondurulmuş OpenAPI'den üretilir: `npm run gen:api`. Test: `npm test`.
- **Erişilebilirlik:** atla bağlantısı, odak halkası, hata mesajlarında `role="alert"`, sade mod. Tam erişilebilirlik denetimi kapsam dışıdır.

Ayrıntı: [docs/wiki/arayuz.md](docs/wiki/arayuz.md)

## Test

```bash
dotnet test Hospital.sln          # arka uç
cd web && npm test                # arayüz
```

Windows Smart App Control yerel derlenen DLL'leri bazen engeller (`0x800711C7`). Bu durumda testleri Linux container'da çalıştırın:
```powershell
./scripts/test-docker.ps1
```

| Katman | Araç | Kapsam |
|---|---|---|
| Birim | xUnit, AwesomeAssertions | State geçişleri, `SlotCalculator`, doğrulayıcılar, koruyucular, e-posta şablonları |
| Entegrasyon | Gerçek PostgreSQL (geçici veritabanı açılır ve silinir), gerçek SMTP (Mailpit) | Exclusion constraint, EF eşlemeleri, outbox, KVKK şifreleme |
| Eşzamanlılık | Gerçek PostgreSQL | N paralel istek aynı slota: tam 1 başarı, N-1 `409` |
| Mimari | NetArchTest | Katman ve feature kuralları |
| API / yetki | `WebApplicationFactory` | 69 uç × 6 kimlik yetki matrisi, IDOR (başkasının kaynağı `404`), bozuk/sahte/süresi dolmuş JWT, refresh rotation |
| Sözleşme | `OpenApiContractTests` | Üretilen OpenAPI dondurulmuş dosyayla aynı mı |
| Performans | `QueryPlanTests` (`EXPLAIN`) | Kritik sorgular indeks kullanır |
| Arayüz | Vitest + Testing Library | Http istemcisi, uygulama akışları, panel akışları, rota erişim matrisi |

Toplam: 700'den fazla arka uç testi (325 birim, 9 mimari, 370 entegrasyon) ve 60 arayüz testi. CI, `main` ve PR'larda PostgreSQL ve Mailpit servisleriyle hepsini çalıştırır; ayrıca web için lint, tip üretimi farkı ve derleme denetlenir. Ayrıntı: [docs/wiki/test-stratejisi.md](docs/wiki/test-stratejisi.md)

## Yapılandırma ve dağıtım

- Yapılandırma anahtarları, anahtar yönetimi, loglar, arka plan işleri ve dağıtım kontrol listesi: [docs/wiki/operasyon.md](docs/wiki/operasyon.md).
- Gizli değerler (`JWT_SIGNING_KEY`, `DATA_PROTECTION_KEY`, `SEED_ADMIN_*`) `.env` veya ortam değişkenleriyle verilir; depoya yazılmaz. **T.C. şifreleme anahtarı kaybolursa kayıtlı kimlik numaraları çözülemez**; yedekleyin.
- Loglama Serilog ile yapılır; her istek `X-Correlation-Id` ile izlenir.
- Docker: `Dockerfile` (API), `web/Dockerfile` (nginx + statik dosyalar), `docker-compose.yml` (PostgreSQL + Mailpit), `docker-compose.app.yml` (API + web).

## Depo yapısı

```
src/
  Core/
    Hospital.Domain          # varlıklar, State'ler, domain olayları, SlotCalculator
    Hospital.Application     # use-case'ler (Features/<Ad>/...), arayüzler, Result
  Infrastructure/
    Hospital.Infrastructure  # dış servisler: parola özeti, e-posta, şifreleme, arka plan işleri
    Hospital.Persistence     # EF Core, AppDbContext, migration'lar, repository somutları
  Presentation/
    Hospital.WebAPI          # Minimal API endpoint'leri, composition root
tests/
  Hospital.UnitTests  Hospital.ArchitectureTests  Hospital.IntegrationTests
web/                         # React + TypeScript + Vite arayüzü
docs/
  api/openapi.v1.json        # dondurulmuş API sözleşmesi
  wiki/                      # proje bilgi tabanı (karar kaydı, mimari, kurallar)
  rapor/                     # dönem proje raporu
  img/                       # README görselleri
scripts/                     # smoke-test, test-docker, openapi güncelleme
```

## Teknoloji yığını

| Alan | Seçim |
|---|---|
| Çalışma zamanı | .NET 9, ASP.NET Core Minimal API |
| Veri | PostgreSQL 16, EF Core 9 + Npgsql, `btree_gist` eklentisi |
| CQRS | Mediator (martinothamar, source generator), FluentValidation |
| Kimlik | JWT Bearer (HS256) + döndürülen refresh token |
| E-posta | MailKit (SMTP), geliştirmede Mailpit |
| Dokümantasyon | Microsoft.AspNetCore.OpenApi + Scalar |
| Loglama | Serilog |
| Test | xUnit, AwesomeAssertions, NetArchTest, Vitest, Testing Library |
| Arayüz | React 19, TypeScript, Vite, React Router, three.js, React Three Fiber, openapi-typescript |
| Dağıtım | Docker, Docker Compose, nginx, GitHub Actions |

Kütüphane seçimlerinin gerekçeleri: [docs/wiki/teknoloji-yigini.md](docs/wiki/teknoloji-yigini.md), [docs/wiki/kararlar.md](docs/wiki/kararlar.md).

## Belgeler

| Belge | İçerik |
|---|---|
| [docs/wiki/index.md](docs/wiki/index.md) | Bilgi tabanı içerik haritası |
| [docs/wiki/plan.md](docs/wiki/plan.md) | Mimari ve kurallar (kaynak spesifikasyon) |
| [docs/wiki/Fazlar.md](docs/wiki/Fazlar.md) | Faz planı (kaynak spesifikasyon) |
| [docs/wiki/faz-plani.md](docs/wiki/faz-plani.md) | Faz ilerleme durumu |
| [docs/wiki/kararlar.md](docs/wiki/kararlar.md) | Mimari karar kaydı (neden bu seçim, neyi reddettik) |
| [docs/wiki/sunum-ozeti.md](docs/wiki/sunum-ozeti.md) | Tek sayfalık mimari özet ve demo akışı |
| [docs/wiki/sunum-raporu.md](docs/wiki/sunum-raporu.md) | Kapsamlı sunum raporu |
| [docs/wiki/operasyon.md](docs/wiki/operasyon.md) | Çalıştırma, yapılandırma, anahtar yönetimi |
| [docs/rapor/](docs/rapor) | BSM515 dönem proje raporu (Word) |

## Geliştirme akışı

- Her iş bir **feature branch**'te yapılır ve **pull request** ile `main`'e birleşir.
- Commit mesajları [Conventional Commits](https://www.conventionalcommits.org/) biçimindedir (`feat`, `fix`, `test`, `docs`, `chore`).
- Bilgi tabanı `docs/wiki/` altında tutulur; her önemli değişiklik `log.md` ve ilgili sayfada kayıt altına alınır. Çalışma kuralları: [CLAUDE.md](CLAUDE.md), [docs/wiki/git-is-akisi.md](docs/wiki/git-is-akisi.md).

## Lisans ve atıflar

Bu depo için ayrı bir lisans dosyası tanımlanmamıştır. Ana sayfadaki 3B modeller Sketchfab üzerinden **CC BY 4.0** lisansıyla, ortam ışığı (HDRI) Poly Haven'dan **CC0** olarak alınmıştır; yazar ve kaynak listesi: [web/public/models/CREDITS.md](web/public/models/CREDITS.md).
