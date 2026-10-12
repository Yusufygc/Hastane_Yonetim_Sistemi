# Yerel Ortam (Docker) ve Geliştirme Ortamı

Kaynak: `plan.md` §12. Bağlı sayfalar: [[teknoloji-yigini]], [[test-stratejisi]], [[veritabani]].

## Planlanan `docker-compose.yml`
- `postgres:16` — kalıcı volume; `btree_gist` migration ile açılır.
- `axllent/mailpit` — SMTP `:1025`, web arayüzü `:8025`.
- (Son fazda) API imajı.

Bağlantı dizesi ve SMTP ayarları `appsettings.Development.json` + `user-secrets`; depoda parola yok.

## Geliştirme makinesi (2026-10-12)
- **.NET:** SDK 9.0.318 ve 10.0.401 yan yana (`C:\Program Files\dotnet`); `global.json` bu repoda SDK 9'u seçer. Yeni terminalde PATH yenilenmemişse `dotnet` bulunamayabilir ([[kararlar]] K-18).
- **Docker:** Docker Desktop çalışıyor. `docker compose up -d` → `hospital-postgres` (5432) ve `hospital-mailpit` (SMTP 1025, UI 8025).
- **Windows Smart App Control: AÇIK (zorlayıcı).** İmzasız NuGet DLL'lerini engelleyebilir; Testcontainers bu yüzden kullanılmıyor ([[kararlar]] K-20). Yeni paket eklerken "Uygulama Denetimi ilkesi bu dosyayı engelledi (0x800711C7)" hatası görülürse ilgili paketi bırakıp alternatif seçin; güvenlik ayarını kapatmayın.
- **SAC tuzağı:** Neredeyse boş (≈4 KB) kendi derlenmiş assembly (ör. içi boş `Hospital.Domain.dll`) test çıktısında aynı 0x800711C7 hatasıyla engellendi; içine gerçek tipler eklenip hash değişince geçti. Kendi DLL'iniz engellenirse önce kodu değiştirip yeniden derleyin, ayarı kapatmayın.
- **Faz 1'de kalıcı engel:** Yeni derlenen `Hospital.Api.dll` hem testhost'ta hem `dotnet run`'da engellendi (CodeIntegrity olay 3077, SAC politikası). Çözüm: derleme/test `./scripts/test-docker.ps1` ile Linux SDK container'ında koşulur (Windows kod bütünlüğü politikası uygulanmaz). Yerel Windows çalıştırması şans eseri çalışabilir. Ayarı kapatmak kullanıcının kararıdır; asistan kapatmaz.
- Dev bağlantı dizesi `appsettings.Development.json` içinde (yalnız yerel geliştirme değeri); üretimde ortam değişkeni/`user-secrets`.
- Entegrasyon testi yönetici bağlantısı: `TEST_POSTGRES_ADMIN` ortam değişkeni (varsayılan localhost:5432).
