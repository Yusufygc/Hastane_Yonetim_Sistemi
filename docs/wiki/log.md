# Wiki Kayıt Defteri

Yeni kayıtlar en üste eklenir. Biçim: `## [YYYY-AA-GG] [İŞLEM_TİPİ] | Kısa Açıklama`. Harita: [[index]].

## [2026-10-13] CODE | Faz 1B tamamlandı: `Appointment` (spike), exclusion constraint'ler, `DataConflictException`→409, 20 paralel istek testi; 41 birim + 9 mimari + 41 entegrasyon test yeşil — [[faz-plani]], [[randevu-kurallari]], [[kararlar]] K-31
## [2026-10-12] DECISION | DI kuralı kaydedildi ve koda uygulandı: `Extensions/<Katman>Ext.cs` + `Add<Katman>`; Program.cs inceltildi; DI kural testleri eklendi — [[di-kurallari]], [[kararlar]] K-30
## [2026-10-12] REFACTOR | Onion yapısına geçildi: `src/Core|Infrastructure|Presentation`, yeni `Hospital.Persistence` projesi, `Hospital.Api` → `Hospital.WebAPI`, Application iç klasörleri (Interfaces, Wrappers, Exceptions, Dto, Parameters), Domain `Entities/`; 7 mimari kural — [[mimari]], [[kararlar]] K-29
## [2026-10-12] CODE | Faz 1 tamamlandı: Entity/Result/pipeline, User+Patient TPT, ilk migration, cookie auth + CSRF + CORS + rate limit + kilitleme, politikalar; 64 test yeşil (container) — [[faz-plani]], [[kimlik-ve-yetki]]
## [2026-10-12] ENV | SAC yeni Hospital.Api.dll'i engelledi; `scripts/test-docker.ps1` ile container'da derleme/test — [[docker-ortam]], [[kararlar]] K-23
## [2026-10-12] DECISION | FluentAssertions yerine AwesomeAssertions (ücretsiz fork) — [[kararlar]] K-21
## [2026-10-12] ENV | .NET 9 SDK (9.0.318) winget ile kuruldu, `global.json` ile sabitlendi; build + testler SDK 9 ile yeşil — [[kararlar]] K-18
## [2026-10-12] DECISION | Testcontainers bırakıldı (Smart App Control engeli); entegrasyon testleri compose PostgreSQL'inde geçici DB kullanır — [[kararlar]] K-20, [[test-stratejisi]]
## [2026-10-12] CODE | Faz 0 tamamlandı: 4 proje + 3 test projesi, CPM, docker-compose, CI, `IEndpoint` taraması, mimari ve endpoint-yetki testleri, `/health` — bkz. [[faz-plani]]
## [2026-10-12] ENV | Ortam çözüldü: .NET 9 runtime ve Docker Desktop hazır (SDK 10 ile `net9.0` derleniyor) — [[docker-ortam]]
## [2026-10-12] ENV (eski) | Ortam taraması: yalnızca .NET 10 SDK kurulu (hedef .NET 9), Docker daemon kapalı — bkz. [[docker-ortam]], [[kararlar]]
## [2026-10-12] SETUP | LLM Wiki kuruldu: `CLAUDE.md` anayasası, [[index]], bu dosya ve konu sayfaları oluşturuldu
## [2026-10-12] INGEST | `plan.md` ve `Fazlar.md` v3.0 içeriği wiki sayfalarına sentezlendi ([[mimari]], [[veritabani]], [[randevu-kurallari]], [[kimlik-ve-yetki]], [[api-uc-noktalari]], [[faz-plani]] vb.)
## [2026-10-12] DECISION | Mimari v3.0: Clean Architecture + feature klasörleri, cookie auth, Guid v7, tek DbContext, doktor-hizmet yönetici onayı — bkz. [[kararlar]]
## [2026-10-12] INGEST | `ProjeOdevi_Son.docx` ödev tanımı okundu — bkz. [[odev-gereksinimleri]]
