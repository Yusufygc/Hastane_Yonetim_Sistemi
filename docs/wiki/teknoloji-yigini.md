# Teknoloji Yığını

Kaynak: `plan.md` §4. Gerekçeler: [[kararlar]]. Yapı: [[mimari]].

| Alan | Seçim | Not |
|---|---|---|
| Çalışma zamanı | .NET 9, ASP.NET Core Minimal API | Ortam durumu için [[docker-ortam]] |
| ORM | EF Core 9 + Npgsql | Sorgularda `AsNoTracking` projeksiyon |
| Veritabanı | PostgreSQL 16 (Docker) | `btree_gist` eklentisi gerekir ([[randevu-kurallari]]) |
| Mediator/CQRS | `Mediator` (martinothamar, source generator) | MediatR v13+ ticari lisanslı, kullanılmıyor |
| Doğrulama | FluentValidation | Pipeline behavior |
| Kimlik doğrulama | **JWT Bearer** (`Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.IdentityModel.JsonWebTokens`), HS256 | Erişim 15 dk + döndürülen refresh token 7 gün; bkz. [[kimlik-ve-yetki]] |
| Parola | `PasswordHasher<T>` (PBKDF2) veya Argon2 | |
| Hata modeli | `Result<T>` + `DomainException`; tek middleware `ProblemDetails` | |
| Kimlik tipi | `Guid` v7 (`Guid.CreateVersion7()`) | Tahmin edilemez, sıralı |
| E-posta | MailKit 4.18.1 (SMTP); geliştirmede Mailpit | [[bildirimler]], [[tasarim-kaliplari]] (Outbox). MailKit 4.14.1 STARTTLS yanıt enjeksiyonu açığı nedeniyle (GHSA-9j88-vvj5-vhgr, 4.16.0'da düzeldi) yamalı sürüm seçildi. |
| Arka plan | `BackgroundService` | Outbox işleyici |
| Loglama | Serilog | Correlation id |
| API dokümanı | `Microsoft.AspNetCore.OpenApi` + Scalar | Swashbuckle yok |
| Test | xUnit, AwesomeAssertions, NetArchTest, Npgsql (gerçek PostgreSQL geçici test DB) | [[test-stratejisi]] |
