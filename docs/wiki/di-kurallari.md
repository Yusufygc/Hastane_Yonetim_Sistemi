# Dependency Injection (DI) Kuralları

Kullanıcı kararı: her katman kendi servislerini **kendi `Extensions/` klasöründe**, `IServiceCollection` genişletme metoduyla kaydeder; `Program.cs` yalnızca bu metotları çağırır. Mimari: [[mimari]]. Karar kaydı: [[kararlar]] (K-30).

## Kural
1. Her katmanda `Extensions/` klasörü bulunur (`<Katman>/Extensions/`, namespace `Hospital.<Katman>.Extensions`).
2. Kayıt metodu **`Add<Katman>`** adıyla `IServiceCollection` üzerinde **extension method** olarak yazılır (`this IServiceCollection services`) ve `IServiceCollection` döner (zincirlenebilir).
3. Metodun bulunduğu statik sınıfın adı **`Ext`** ile biter: `ApplicationExt`, `InfrastructureExt`, `PersistenceExt`, `WebApiExt` (dosya adı = sınıf adı).
4. Katmanlar kendi içinde DI ayrıntısını gizler; `Program.cs` yalnızca ilgili `Add...` metotlarını çağırır, tek tek servis kaydı yapmaz.
5. Yeni bir feature için kayıt gerekiyorsa önce otomatik tarama (aşağıda) yeterli mi bakılır; değilse ilgili katmanın `Ext` sınıfına eklenir. `Program.cs` değişmez.

## Katman → metot eşlemesi
| Katman | Dosya | Metot | Ne kaydeder |
|---|---|---|---|
| Application | `Hospital.Application/Extensions/ApplicationExt.cs` | `AddApplication()` | Mediator (+ loglama/doğrulama behavior'ları), FluentValidation validator taraması, `TimeProvider` |
| Application | `.../Extensions/FeatureRegistrationExt.cs` | `AddFeatureImplementations(assembly, ns)` | Yardımcı: `Features/**` somutlarını `Hospital.Application.*` arayüzleriyle tarayıp kaydeder |
| Infrastructure | `Hospital.Infrastructure/Extensions/InfrastructureExt.cs` | `AddInfrastructure()` | Dış servisler (`Features/**` taranır: parola özeti, e-posta…) |
| Persistence | `Hospital.Persistence/Extensions/PersistenceExt.cs` | `AddPersistence()` | `AppDbContext` (Npgsql, snake_case), `IUnitOfWork`, `Features/**` taraması (repository, okuma servisleri) |
| WebAPI | `Hospital.WebAPI/Extensions/WebApiExt.cs` | `AddWebApi(configuration)` | Endpoint taraması, `ICurrentUser`, `AuthSettings`/`JwtSettings`, JWT Bearer + politikalar, CORS, rate limit, ProblemDetails/hata işleyici, health check, OpenAPI (Bearer şemalı) |
| WebAPI | `.../Extensions/WebApiExt.cs` | `UseWebApi()` | İstek hattı (middleware) sırası ve endpoint eşlemeleri |

WebAPI içindeki alt kayıt yardımcıları da aynı kuralla `Extensions/` altında durur: `AuthenticationExt` (JWT Bearer, politikalar, CORS, rate limit), `OpenApiExt` (Bearer şeması), `EndpointExt` (`IEndpoint` taraması), `DatabaseMigrationExt`.

## Program.cs şablonu
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddPersistence();
builder.Services.AddWebApi(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.MigrateDatabaseIfConfiguredAsync();
app.UseWebApi();
app.Run();
```
Katman sırası: iç → dış (Application, Infrastructure, Persistence, WebAPI). Application'daki servisler dış katmanların somutlarına bağımlı değildir; yalnızca arayüz kullanır.

## Otomatik tarama (yeni feature kayıt dosyasını değiştirmez)
- `Persistence/Features/**` ve `Infrastructure/Features/**` altındaki somut sınıflar, uyguladıkları `Hospital.Application.*` arayüzleriyle **scoped** olarak kaydolur (`UserRepository` → `IUserRepository`).
- Handler'lar Mediator source generator ile, validator'lar `AddValidatorsFromAssembly` ile, endpoint'ler `IEndpoint` taramasıyla, EF konfigürasyonları `ApplyConfigurationsFromAssembly` ile bulunur.
- Taramanın dışında kalan kayıtlar (ör. `UnitOfWork`, `AppDbContext`) ilgili `Ext` sınıfında açıkça yapılır.

## Kuralın test edilmesi
`DependencyInjectionConventionTests` (Hospital.ArchitectureTests):
- `IServiceCollection` genişletme metodu içeren her statik sınıf `*.Extensions` namespace'inde ve adı `Ext` ile bitmeli.
- Her katman kendi `Add...` metodunu sunmalı (`AddApplication`, `AddInfrastructure`, `AddPersistence`, `AddWebApi`).
Bkz. [[test-stratejisi]].

## Yeni katman/eklenti eklerken
1. `Extensions/<Ad>Ext.cs` oluştur, `Add<Ad>` metodunu yaz.
2. `Program.cs`'e tek satır ekle: `builder.Services.Add<Ad>();`.
3. Gerekirse `DependencyInjectionConventionTests` beklenen metot listesine ekle.
