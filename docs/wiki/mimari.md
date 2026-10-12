# Mimari

Clean Architecture + feature klasörlü monolit. Kaynak: `plan.md` §3. Kararların gerekçesi: [[kararlar]]. Kütüphaneler: [[teknoloji-yigini]].

## Katmanlar (4 ayrı proje)
```
Hospital.Api (Presentation) ─┐
                             ├─► Hospital.Application ─► Hospital.Domain
Hospital.Infrastructure ─────┘
```
- **Hospital.Domain:** entity'ler, state sınıfları, domain event'ler. Hiçbir şeye bağımlı değil. Bkz. [[domain-modeli]].
- **Hospital.Application:** Command/Query/Handler/Validator, DTO, soyutlamalar (`IAppointmentRepository`, `IEmailSender`, `ICurrentUser`).
- **Hospital.Infrastructure:** EF Core, repository somutları, MailKit, outbox işleyici, kimlik servisleri.
- **Hospital.Api:** Minimal API endpoint'leri, middleware, auth yapılandırması, composition root. Bkz. [[api-uc-noktalari]].

## Feature bazlı klasörleme
Her katmanda **aynı feature adlarını yansıtan** `Features/<Ad>/` klasörü bulunur. Handler'lar tek yerde toplanmaz.

```
Hospital.Application/Features/Appointments/
  Commands/CreateAppointment/   (Command, Handler, Validator)
  Commands/ApproveAppointment/
  Queries/GetAvailableSlots/    (Query, Handler, Dto)
  IAppointmentRepository.cs
Hospital.Infrastructure/Features/Appointments/
  AppointmentRepository.cs, AppointmentConfiguration.cs
Hospital.Api/Features/Appointments/
  AppointmentEndpoints.cs
```
Feature listesi: Auth, Appointments, Doctors (Profile, Availability, TimeOff, Services), Patients, Staff, Admins, Organizations, Hospitals, Departments, Services, Catalog, Notifications.

## Kurallar
- Soyutlama Application'da, somut sınıf Infrastructure'da (aynı feature adı).
- Bir feature başka bir feature'ın **handler'ına** bağlanmaz; gerekirse onun soyutlamasını kullanır.
- Otomatik kayıt: handler'lar (Mediator), endpoint'ler (`IEndpoint` taraması), EF konfigürasyonları (`IEntityTypeConfiguration` taraması). Böylece `Program.cs` ve `AppDbContext` yeni feature için değişmez ([[git-is-akisi]] çakışma azaltma).
- Tek `AppDbContext`, tek PostgreSQL veritabanı, gerçek FK'ler ([[veritabani]]).
- Infrastructure servis kaydı: `Infrastructure/Features/**` altındaki somut sınıflar, uyguladıkları `Hospital.Application.*` arayüzleriyle taranıp otomatik kaydolur (ör. `UserRepository` → `IUserRepository`); `AddInfrastructure()` yeni feature için değişmez. Bağlantı dizesi çalışma anında çözülür (testler geçersiz kılabilsin).
- `ICurrentUser` uygulaması Api katmanındadır (`HttpContext` claim'leri).

## Mimari testler (NetArchTest)
Domain hiçbir projeye bağımlı değil; Application → Infrastructure/Api'ye bağımlı değil; Infrastructure → Api'ye bağımlı değil; feature'lar birbirinin handler/command tiplerine bağlanmaz; her endpoint açık politika veya `AllowAnonymous` taşır. Bkz. [[test-stratejisi]], [[kimlik-ve-yetki]].

İlgili: [[tasarim-kaliplari]], [[faz-plani]].
