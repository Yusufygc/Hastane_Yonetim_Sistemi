# Yerel Ortam (Docker) ve Geliştirme Ortamı

Kaynak: `plan.md` §12. Bağlı sayfalar: [[teknoloji-yigini]], [[test-stratejisi]], [[veritabani]].

## Planlanan `docker-compose.yml`
- `postgres:16` — kalıcı volume; `btree_gist` migration ile açılır.
- `axllent/mailpit` — SMTP `:1025`, web arayüzü `:8025`.
- (Son fazda) API imajı.

Bağlantı dizesi ve SMTP ayarları `appsettings.Development.json` + `user-secrets`; depoda parola yok.

## Geliştirme makinesi (2026-10-12)
- **.NET:** SDK 10.0.401, runtime 9.0.20 ve 10.0.12 (`C:\Program Files\dotnet`). Hedef `net9.0`; SDK 10 ile derlenir, runtime 9 ile çalışır. Yeni terminalde PATH yenilenmemişse `dotnet` bulunamayabilir ([[kararlar]] K-18).
- **Docker:** Docker Desktop çalışıyor. `docker compose up -d` → `hospital-postgres` (5432) ve `hospital-mailpit` (SMTP 1025, UI 8025).
- **Windows Smart App Control: AÇIK (zorlayıcı).** İmzasız NuGet DLL'lerini engelleyebilir; Testcontainers bu yüzden kullanılmıyor ([[kararlar]] K-20). Yeni paket eklerken "Uygulama Denetimi ilkesi bu dosyayı engelledi (0x800711C7)" hatası görülürse ilgili paketi bırakıp alternatif seçin; güvenlik ayarını kapatmayın.
- Dev bağlantı dizesi `appsettings.Development.json` içinde (yalnız yerel geliştirme değeri); üretimde ortam değişkeni/`user-secrets`.
- Entegrasyon testi yönetici bağlantısı: `TEST_POSTGRES_ADMIN` ortam değişkeni (varsayılan localhost:5432).
