# Mimari

Onion (Clean) Architecture + feature klasörlü monolit. Kaynak: `plan.md` §3. Kararların gerekçesi: [[kararlar]]. Kütüphaneler: [[teknoloji-yigini]].

## Katmanlar (Onion — 5 proje)
Bağımlılıklar yalnızca içeriye doğrudur:
```
Hospital.WebAPI ──► Hospital.Application ──► Hospital.Domain
        │                    ▲  ▲
        ├──► Hospital.Infrastructure ──┘  │
        └──► Hospital.Persistence ────────┘
```
| Halka | Proje | İçerik |
|---|---|---|
| Core | `Hospital.Domain` | `Common/` (Entity, AggregateRoot, DomainEvent…), `Entities/` (Users, Facility, Appointments). Hiçbir şeye bağımlı değil. Bkz. [[domain-modeli]]. |
| Core | `Hospital.Application` | `Features/` (use-case'ler), `Interfaces/`, `Wrappers/` (Result), `Exceptions/`, `Behaviors/`, `Security/` (Policies), `Dto/`, `Parameters/`. |
| Infrastructure | `Hospital.Infrastructure` | Veri tabanı dışı dış servisler: parola özeti, e-posta, outbox işleyici. |
| Infrastructure | `Hospital.Persistence` | `Context/` (AppDbContext), `Migrations/`, `UnitOfWorks/`, `Features/` (repository, EF konfigürasyonu, okuma servisleri). |
| Presentation | `Hospital.WebAPI` | Minimal API endpoint'leri, middleware, auth yapılandırması, composition root. Bkz. [[api-uc-noktalari]]. |

- `Infrastructure` ve `Persistence` yalnız `Application`'a bağımlı; **birbirini bilmez**.
- `Application`, EF Core/Npgsql/ASP.NET Core'u bilmez (persistence ignorance).

## Fiziksel klasör hiyerarşisi
```
src/
  Core/            Hospital.Domain, Hospital.Application
  Infrastructure/  Hospital.Infrastructure, Hospital.Persistence
  Presentation/    Hospital.WebAPI
tests/             Hospital.UnitTests, Hospital.ArchitectureTests, Hospital.IntegrationTests
web/               React + TypeScript + Vite arayüz (Faz 7; bkz. [[arayuz]])
```
`web/` backend katmanlarının dışındadır; backend ile tek bağı dondurulmuş OpenAPI sözleşmesidir (`docs/api/openapi.v1.json`, [[operasyon]]). Bütünsel anlatım: [[sunum-raporu]].

## Feature bazlı klasörleme
Her katmanda **aynı feature adlarını yansıtan** `Features/<Ad>/` klasörü bulunur; handler'lar tek yerde toplanmaz. Feature'lar: Auth, Organizations, Hospitals, Departments, Services, Catalog, **Doctors, StaffMembers, Admins, Users, Patients** (Faz 3; `StaffMembers` adı K-47), **Appointments** (Faz 4), **Notifications** (Faz 5; olay dinleyicileri, outbox sözleşmeleri, şablonlar — [[bildirimler]]). Feature'lar arası ortak doğrulama kuralları `Application/Validation/AccountRules.cs`.
```
Hospital.Application/Features/Appointments/
  Commands/CreateAppointment/   (Command, Handler, Validator)
  Commands/ApproveAppointment/
  Queries/GetAvailableSlots/    (Query, Handler, Dto)
  IAppointmentRepository.cs
Hospital.Persistence/Features/Appointments/
  AppointmentRepository.cs, AppointmentConfiguration.cs
Hospital.WebAPI/Features/Appointments/
  AppointmentEndpoints.cs
```
Feature listesi: Auth, Appointments, Doctors (Profile, Availability, TimeOff, Services), Patients, Staff, Admins, Organizations, Hospitals, Departments, Services, Catalog, Notifications.

## Kurallar
- Soyutlama Application'da (feature klasörü), somut sınıf Persistence/Infrastructure'da (aynı feature adı).
- Bir feature başka bir feature'ın **handler'ına** bağlanmaz; gerekirse onun soyutlamasını kullanır.
- Otomatik kayıt: handler'lar (Mediator), endpoint'ler (`IEndpoint` taraması), EF konfigürasyonları (`IEntityTypeConfiguration` taraması), Persistence/Infrastructure somutları (`Features/**` taranıp uyguladıkları `Hospital.Application.*` arayüzleriyle). Böylece `Program.cs`, `AppDbContext`, `Extensions/*Ext` yeni feature için değişmez ([[git-is-akisi]] çakışma azaltma). Tarama yardımcısı: `Application/Extensions/FeatureRegistrationExt.AddFeatureImplementations`.
- **DI kuralı:** her katman `Extensions/<Katman>Ext.cs` içinde `Add<Katman>` ile kaydolur; `Program.cs` yalnızca bunları çağırır → [[di-kurallari]].
- Tek `AppDbContext`, tek PostgreSQL veritabanı, gerçek FK'ler ([[veritabani]]). Bağlantı dizesi çalışma anında çözülür (testler geçersiz kılabilsin).
- `ICurrentUser` uygulaması WebAPI'dedir (`HttpContext` claim'leri). `IUnitOfWork` uygulaması `Persistence/UnitOfWorks`'tedir (DbContext üzerinden).

## Mimari testler (NetArchTest)
- Domain hiçbir katmana ve altyapı kütüphanesine (EF, Npgsql, ASP.NET, Mediator, FluentValidation) bağımlı değil.
- Application → Infrastructure/Persistence/WebAPI'ye ve EF/Npgsql/ASP.NET'e bağımlı değil.
- Infrastructure ↔ Persistence birbirine bağımlı değil; ikisi de WebAPI'ye bağımlı değil.
- Feature'lar birbirinin handler/command tiplerine bağlanmaz.
- Her endpoint açık politika veya `AllowAnonymous` taşır.
Bkz. [[test-stratejisi]], [[kimlik-ve-yetki]].

İlgili: [[tasarim-kaliplari]], [[faz-plani]].
