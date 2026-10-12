# Hastane / Klinik Randevu Sistemi

Çok şubeli (veya tek şubeli) hastane/klinik randevu yönetim sistemi. .NET 9, Clean Architecture + feature klasörleri, PostgreSQL.

- Mimari ve kurallar: [plan.md](plan.md)
- Faz planı: [Fazlar.md](Fazlar.md)
- Bilgi tabanı (wiki): [docs/wiki/index.md](docs/wiki/index.md)

## Gereksinimler
- .NET 9 SDK (`global.json` 9.0.x'e sabitler)
- Docker Desktop (PostgreSQL ve Mailpit için; entegrasyon testleri `docker compose up -d` ile ayağa kalkan PostgreSQL'i kullanır)

## Çalıştırma
```bash
docker compose up -d
dotnet run --project src/Hospital.Api
```
- Sağlık ucu: http://localhost:5284/health
- API dokümanı (Development): http://localhost:5284/scalar/v1
- Mailpit arayüzü: http://localhost:8025

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
  Hospital.Domain          # entity'ler, domain kuralları
  Hospital.Application     # use-case'ler (Features/<Ad>/...), soyutlamalar
  Hospital.Infrastructure  # EF Core, repository somutları, e-posta, kimlik servisleri
  Hospital.Api             # Minimal API endpoint'leri, composition root
tests/
  Hospital.UnitTests  Hospital.ArchitectureTests  Hospital.IntegrationTests
```
