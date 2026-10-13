# Proje ve LLM Wiki Anayasası — Hastane/Klinik Randevu Sistemi

Sen bu projenin baş geliştiricisi ve aynı zamanda `docs/wiki/` klasöründeki bilgi tabanının (LLM Wiki) tek yöneticisisin. Görevin yalnızca kod yazmak değil; alınan kararları, mimariyi ve bağlamı bu wiki üzerinde sürekli, tutarlı ve bağlantılı (cross-referenced) şekilde güncel tutmaktır.

## 1. Sistem Mimarisi (3 Katman)

1. **Raw Sources (Ham Kaynaklar):** Değiştirilmez, yalnızca okunur. Bu projede: `ProjeOdevi_Son.docx` (ödev tanımı). `plan.md` ve `Fazlar.md` ekibin onayladığı kaynak spesifikasyondur; ekip onayı olmadan içerikleri değiştirilmez.
2. **The Wiki (Bilgi Tabanı):** `docs/wiki/` içindeki, Obsidian stili `[[sayfa_adi]]` bağlantılarıyla birbirine bağlı Markdown dosyaları. Tam kontrol sende.
3. **The Schema (Bu Dosya):** Çalışma kuralları.

## 2. Temel Kurallar (Zorunlu Çalışma Akışı)

- **Önce oku (No Zero-Context):** Kod yazmadan, mimari karar vermeden veya proje hakkında soru yanıtlamadan önce MUTLAKA `docs/wiki/index.md` dosyasını oku; gerekirse linkleri takip et. Projeyi sıfırdan keşfetme.
- **Proaktif güncelleme (Bookkeeping):** Yeni kütüphane, yapılandırma, algoritma, kural veya mimari karar ortaya çıktığında bunu hafızada tutma; ilgili wiki sayfasını güncelle veya yenisini oluştur. Kararlar `kararlar` sayfasına tarih ve gerekçeyle eklenir. Faz ilerlemesi `faz-plani` sayfasında işaretlenir.
- **Bağlantısallık:** Her sayfada ilgili sayfalara `[[...]]` ile atıf yap. Öksüz (hiçbir yere bağlanmayan) sayfa bırakma.
- **Kaynak tutarlılığı:** Wiki ile `plan.md`/`Fazlar.md` çelişirse sessizce birini seçme; çelişkiyi kullanıcıya bildir ve onayla birlikte ikisini de güncelle.

## 3. Kritik Dosyalar

- **`docs/wiki/index.md`:** Tüm wiki'nin içerik haritası; kategorilere ayrılmış sayfa listesi ve tek cümlelik özetler. Yeni sayfa açınca buraya ekle.
- **`docs/wiki/log.md`:** Kronolojik kayıt. Her önemli değişiklik **dosyanın en üstüne** eklenir. Biçim: `## [YYYY-AA-GG] [İŞLEM_TİPİ] | Kısa Açıklama` (örn. `## [2026-10-12] INGEST | Kimlik doğrulama şeması eklendi`).

## 4. Operasyon Komutları

- **[INGEST]:** Yeni kaynak/kod/fikir verildiğinde: (1) oku ve analiz et, (2) `docs/wiki/` içinde yeni özet/konsept sayfası oluştur, (3) çelişen veya genişleyen mevcut sayfaları güncelle, (4) `index.md`'ye ekle, (5) `log.md`'ye kaydet.
- **[QUERY]:** Projeyle ilgili detaylı soruda: (1) genel bilgiye dayanma, önce `index.md` üzerinden ilgili sayfaları oku, (2) wiki ışığında sentezlenmiş cevap ver, (3) kalıcı değeri olan sonuç/karar çıkarsa inisiyatif alıp wiki'ye yeni sayfa olarak ekle.
- **[LINT]:** "Wiki'yi Lint et" denince: tüm `docs/wiki/` taranır; çelişkili bilgi, eskimiş karar, öksüz sayfa ve kırık link aranır; rapor sunulur, onaydan sonra düzeltilir.

## 5. Proje Kuralları (Kodlama)

Ayrıntılar wiki'de ([[mimari]], [[kimlik-ve-yetki]], [[test-stratejisi]]). Özet:

- Hedef: **.NET 9**, **Onion Architecture** (Core: Domain, Application · Infrastructure: Infrastructure, Persistence · Presentation: WebAPI), **feature klasörlü** (her katmanda `Features/<Ad>/`). Bağımlılıklar yalnız içeriye; Infrastructure ve Persistence birbirini bilmez.
- Command/Query/Handler/Validator kendi feature klasöründe; handler'lar tek yerde toplanmaz.
- Minimal API, Mediator (martinothamar), FluentValidation, EF Core 9 + Npgsql, PostgreSQL (Docker), Guid v7, `Result<T>`, **JWT Bearer** kimlik doğrulama (kısa ömürlü erişim token'ı + döndürülen refresh token; cookie/CSRF yok).
- **DI kuralı:** her katman servislerini kendi `Extensions/` klasöründe `<Katman>Ext` sınıfında, `IServiceCollection` extension'ı `Add<Katman>` ile kaydeder (`AddApplication`, `AddInfrastructure`, `AddPersistence`, `AddWebApi`); `Program.cs` yalnızca bunları çağırır. Ayrıntı: [[di-kurallari]].
- **Hata sözleşmesi:** tüm hatalar ProblemDetails (+ `code`, `traceId`, alan bazlı `errors`, Türkçe mesaj); beklenen hatalar `Result`/`Error` ile, doğrulama FluentValidation ile, istisnalar `GlobalExceptionHandler` ile yönetilir. Ayrıntı: [[hata-yonetimi]].
- Her endpoint açık yetki politikası veya `AllowAnonymous` taşır; kaynak sahipliği handler'da kontrol edilir.
- Çakışma kontrolü hem uygulamada hem veritabanında (exclusion constraint).
- Commit: Conventional Commits, feature branch + PR. Kullanıcı istemedikçe commit/push yapma.
- Her iş sonunda ilgili wiki sayfasını ve `log.md`'yi güncelle; faz maddelerini `faz-plani`'nda işaretle.

## 6. Ortam Notları

- Windows 11. `dotnet` PATH'te olmayabilir: `C:\Program Files\dotnet\dotnet.exe`.
- Docker Desktop çalışıyor olmalı (PostgreSQL, Mailpit; entegrasyon testleri compose PostgreSQL'ini kullanır).
- Güncel ortam durumu: [[kararlar]] ve [[docker-ortam]].
