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
- [[bildirimler]] — E-posta bildirimleri: olay → outbox → işleyici → SMTP, şablonlar, yeniden deneme, yapılandırma, hata ayıklama.

## API ve Güvenlik
- [[kimlik-ve-yetki]] — JWT Bearer + refresh token (rotation), CORS, politikalar, kaynak sahipliği, kapsam.
- [[api-uc-noktalari]] — Endpoint listesi ve yetki politikaları.
- [[hata-yonetimi]] — Hata yanıt biçimi (ProblemDetails + `code` + `errors`), hata kodları tablosu, doğrulama ve istisna bileşenleri.
- [[api-ornekleri]] — curl ve Scalar ile kayıt/giriş/Bearer akışı, tuzaklar ve `scripts/smoke-test.sh`.

## Kalite ve Ortam
- [[test-stratejisi]] — Test katmanları ve zorunlu senaryolar.
- [[docker-ortam]] — Yerel ortam (PostgreSQL, Mailpit) ve geliştirme ortamı notları.
- [[operasyon]] — Çalıştırma/dağıtım (Dockerfile, compose), yapılandırma anahtarları, anahtar yönetimi, loglar, arka plan işleri, indeksler, dondurulmuş API sözleşmesi, yetki matrisi.
- [[sunum-ozeti]] — Ödev sunumu için tek sayfalık backend mimari özeti ve demo akışı.
