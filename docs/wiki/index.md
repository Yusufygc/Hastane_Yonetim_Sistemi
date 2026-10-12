# Wiki İçerik Haritası

Hastane/Klinik Randevu Sistemi bilgi tabanı. Kurallar için kök dizindeki `CLAUDE.md` (anayasa). Kronoloji: [[log]].

## Proje
- [[odev-gereksinimleri]] — Ödevin (Seçenek A) zorunlu gereksinimleri, teslim kuralları ve gereksinim → çözüm eşlemesi.
- [[faz-plani]] — Geliştirme fazları, çıkış kriterleri ve ilerleme durumu.
- [[git-is-akisi]] — Branch, commit ve ekip çalışma kuralları.

## Mimari
- [[mimari]] — Onion mimari (5 proje), klasör hiyerarşisi, feature klasörleme, mimari testler.
- [[teknoloji-yigini]] — Kullanılan kütüphaneler ve gerekçeleri.
- [[di-kurallari]] — Katman başına `Extensions/<Katman>Ext.cs` ile `Add...` DI kaydı; Program.cs şablonu.
- [[tasarim-kaliplari]] — Repository/UoW, CQRS, State, Domain Events + Outbox.
- [[kararlar]] — Mimari karar kaydı (neden bu seçim, neyi reddettik).

## Veri ve Domain
- [[domain-modeli]] — Kullanıcı hiyerarşisi (TPT), kapsülleme, State.
- [[veritabani]] — ER özeti, bütünlük kuralları, indeksler.
- [[randevu-kurallari]] — Slot üretimi, çakışma koruması, durum makinesi, doktor-hizmet onayı.

## API ve Güvenlik
- [[kimlik-ve-yetki]] — Cookie auth, CSRF/CORS, politikalar, kaynak sahipliği.
- [[api-uc-noktalari]] — Endpoint listesi ve yetki politikaları.

## Kalite ve Ortam
- [[test-stratejisi]] — Test katmanları ve zorunlu senaryolar.
- [[docker-ortam]] — Yerel ortam (PostgreSQL, Mailpit) ve geliştirme ortamı notları.
