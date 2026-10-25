# Hastane / Klinik Randevu Sistemi

Çok şubeli (veya tek şubeli) hastane/klinik randevu yönetim sistemi. .NET 9, Onion Architecture + feature klasörleri, PostgreSQL.

- Mimari ve kurallar: [plan.md](plan.md)
- Faz planı: [Fazlar.md](Fazlar.md)
- Bilgi tabanı (wiki): [docs/wiki/index.md](docs/wiki/index.md)

## Gereksinimler
- .NET 9 SDK (`global.json` 9.0.x'e sabitler)
- Docker Desktop (PostgreSQL ve Mailpit için; entegrasyon testleri `docker compose up -d` ile ayağa kalkan PostgreSQL'i kullanır)

## Çalıştırma
```bash
docker compose up -d
dotnet run --project src/Presentation/Hospital.WebAPI
```
- Sağlık ucu: http://localhost:5284/health
- API dokümanı (Development): http://localhost:5284/scalar/v1
- Kimlik doğrulama JWT Bearer: `POST /api/auth/login` → `accessToken`; Scalar'da **Authorize** alanına yapıştırın (ayrıntı: docs/wiki/api-ornekleri.md)
- Mailpit arayüzü: http://localhost:8025 (randevu e-postaları burada görünür; bkz. docs/wiki/bildirimler.md)
- Geliştirmede ilk açılışta varsayılan kurum/şube, örnek bölüm-hizmetler ve ilk süper yönetici otomatik oluşur; giriş bilgileri `appsettings.Development.json` içindeki `Seed:SuperAdmin` bölümündedir (üretimde `Seed__SuperAdmin__Email/Password` ortam değişkenleriyle verilir).

### Her şey container'da (PostgreSQL + Mailpit + API)
```bash
cp .env.example .env        # gizli değerleri (JWT_SIGNING_KEY, DATA_PROTECTION_KEY, SEED_ADMIN_*) düzenleyin
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```
API: http://localhost:8080 (sağlık: `/health`). Yapılandırma anahtarları, anahtar yönetimi (T.C. şifreleme anahtarını yedekleyin), loglar ve dağıtım listesi: [docs/wiki/operasyon.md](docs/wiki/operasyon.md).

## API sözleşmesi
- Dondurulmuş OpenAPI belgesi: [docs/api/openapi.v1.json](docs/api/openapi.v1.json) (bilinçli değişiklikten sonra `./scripts/update-openapi-snapshot.ps1`).
- Hata biçimi ProblemDetails (+ `code`, `traceId`, `errors`); her yanıtta `X-Correlation-Id`; liste uçlarında `page`/`pageSize` ve `X-Total-Count` başlığı.
- Mimari özeti (sunum): [docs/wiki/sunum-ozeti.md](docs/wiki/sunum-ozeti.md)

## Test
```bash
dotnet test Hospital.sln
```
Windows Smart App Control yerel derlenen DLL'leri bazen engelleyebilir (`0x800711C7`). Bu durumda testleri Linux container'da çalıştırın:
```powershell
./scripts/test-docker.ps1
```
Entegrasyon testleri, `docker compose up -d` ile çalışan PostgreSQL üzerinde geçici bir veritabanı açıp siler.

## Çözüm yapısı
```
src/
  Core/
    Hospital.Domain          # entity'ler (Entities/), domain kuralları
    Hospital.Application     # use-case'ler (Features/<Ad>/...), arayüzler, Result
  Infrastructure/
    Hospital.Infrastructure  # dış servisler: parola özeti, e-posta
    Hospital.Persistence     # EF Core, AppDbContext, migration'lar, repository somutları
  Presentation/
    Hospital.WebAPI          # Minimal API endpoint'leri, composition root
tests/
  Hospital.UnitTests  Hospital.ArchitectureTests  Hospital.IntegrationTests
```
