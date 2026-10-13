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
- Mailpit arayüzü: http://localhost:8025
- Geliştirmede ilk açılışta varsayılan kurum/şube, örnek bölüm-hizmetler ve ilk süper yönetici otomatik oluşur; giriş bilgileri `appsettings.Development.json` içindeki `Seed:SuperAdmin` bölümündedir (üretimde `Seed__SuperAdmin__Email/Password` ortam değişkenleriyle verilir).

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
