# Domain Modeli ve OOP

Kaynak: `plan.md` §5. Tablolar: [[veritabani]]. Kalıplar: [[tasarim-kaliplari]].

## Kullanıcı hiyerarşisi (kalıtım + çok biçimlilik)
```
Entity<TId>
 └─ User (abstract)      // Email, PasswordHash, IsActive, Role (abstract), SecurityStamp
     ├─ Patient          // TC, kan grubu, doğum tarihi
     ├─ Doctor           // uzmanlık, diploma, bölüm
     ├─ Staff            // ünvan, şube/bölüm
     └─ Admin            // erişim seviyesi, organizasyon/şube kapsamı
```
EF Core **gerçek Table Per Type (TPT)**: `USER_ACCOUNT` taban tablo; alt tipler **aynı PK'yı paylaşır** (`PATIENT.id` = `USER_ACCOUNT.id` FK). Bir hesap tam olarak tek türdendir. Eski ER'deki ayrı `id` + `user_account_id` modeli, bir hesaba birden çok profil ve `role` çelişkisi doğurduğu için bırakıldı ([[kararlar]]).

## Kapsülleme
- `private set`; durum değişikliği davranış metoduyla (`appointment.Approve(actor, now)`).
- Kurallar domain'de korunur, Application yalnız orkestrasyon yapar ([[mimari]]).
- `AggregateRoot` domain event listesi tutar; olaylar `SaveChanges` sırasında dağıtılır.

## Aggregate'ler
Gerçekleşen: `User` → `Patient`, `Admin`, **`Doctor`** (+`DoctorServiceLink`, `DoctorAvailability`, `DoctorTimeOff`), **`Staff`** · `Organization` · **`HospitalBranch`** (+`HospitalWorkingHours` çocuk varlıkları) · `Department` · `Service` · `Appointment` (Faz 1B çekirdeği). `Appointment` artık tam aggregate (Faz 4). Kurallar: [[randevu-kurallari]].

## Kurum varlıkları (Faz 2)
- **`HospitalBranch`** = ER'deki HOSPITAL (tablo `hospital`, API `/hospitals`). Sınıf adı kök namespace `Hospital` ile çakıştığı için bu ad kullanıldı ([[kararlar]] K-32). `SetWorkingHours` listeyi **eşitler** (mevcut gün güncellenir, yeni gün eklenir, listede olmayan kaldırılır; önce doğrular, sonra uygular → atomik). `TimeZoneId` IANA kimliği olarak doğrulanır.
- **`Service`**: sabit süre (1–480 dk), `numeric(12,2)` ücret (negatif olamaz), 3 harfli para birimi (büyük harfe çevrilir). Bölüm opsiyoneldir; verilirse aynı şubeye ait ve aktif olmalıdır (handler doğrular).
- **`Department`/`Service`/`HospitalBranch`** silinmez, `Deactivate()` ile pasifleştirilir. Aktif hizmeti olan bölüm pasifleştirilemez.
- **`Admin`**: `AdminAccessLevel` (`SuperAdmin` = organizasyon kapsamı, `HospitalId` boş; `BranchManager` = tek şube). `ScopeClaims` çok biçimli olarak `organization_id`, `access_level`, (`hospital_id`) üretir; JWT erişim belirtecine yazılır ([[kimlik-ve-yetki]]).
## Çalışan ve profil varlıkları (Faz 3)
- **`Doctor : User`** (tablo `doctor`, PK = `user_account.id`): ad, soyad, bölüm (dolayısıyla şube), uzmanlık, diploma no, telefon. Üç alt koleksiyonu **kendi içinden** yönetir (kapsülleme; dışarıdan liste değiştirilemez): `Services`, `Availabilities`, `TimeOffs`. Aggregate sınırı doktordur; handler ilgili koleksiyonu `DoctorInclude` bayraklarıyla yükler.
- **Hizmet talep/onay (`DoctorServiceLink`, tablo `doctor_service`, bileşik PK doctor+service):** `RequestService` (PENDING; REJECTED ise yeniden açılır; PENDING/APPROVED ise hata) → `ApproveService` / `RejectService` (yalnız PENDING reddedilir) · `AssignService` (yönetici, onay adımı olmadan APPROVED, idempotent) · `RemoveService`. Yalnız APPROVED kayıtlar randevu alınabilirliğini belirler ([[randevu-kurallari]]). Geçiş metotları `internal`: dışarıdan yalnız `Doctor` üzerinden değişir.
- **Uygunluk (`DoctorAvailability`):** haftalık tekrar eden pencere (gün + `TimeOnly` aralığı) + geçerlilik dönemi (`ValidFrom` zorunlu, `ValidTo` boşsa süresiz). Kurallar `Doctor` içinde: bitiş > başlangıç, `ValidTo ≥ ValidFrom`, pencere şubenin o günkü çalışma saatleri içinde (şube o gün kapalıysa reddedilir), aynı gün + kesişen saat + kesişen dönem → çakışma (bitişik pencere çakışmaz; yarı açık `[)`). Güncellemede kendisi çakışma sayılmaz.
- **İzin (`DoctorTimeOff`):** UTC `[StartAt, EndAt)` aralığı; bitişi geçmişte olan izin eklenemez; kesişen izin reddedilir.
- **`Staff : User`** (tablo `staff`): ad, soyad, ünvan, şube, opsiyonel bölüm. Randevu işine karışmaz.
- **`Patient.UpdateProfile`:** ad, soyad, telefon, kan grubu (A+, A-, B+, B-, AB+, AB-, 0+, 0-); T.C. ve doğum tarihi değiştirilemez. **`TurkishIdentityNumber.Mask`:** ilk 3 + son 2 hane görünür (`100******46`).
- Domain kuralları ihlalinde `DomainException` fırlatır; handler'lar beklenen durumları önceden denetleyip `Result`/`Error` döner (Türkçe `code`'lu), `DomainException` ikinci savunmadır ([[hata-yonetimi]]).

## Randevu aggregate'i (Faz 4)
- **`Appointment`** (`AggregateRoot`): şube, hasta, doktor, hizmet kimlikleri; `[StartAt, EndAt)` UTC; `Status`; **snapshot** (`ServiceNameSnapshot`, `DurationMinutesSnapshot`, `PriceSnapshot`, `CurrencySnapshot`); `PatientNote`, `DecisionReason`, `StatusChangedBy`, `CreatedAt/UpdatedAt`. Oluşturma tek yoldan: `Appointment.Request(...)` (+ `ServiceSnapshot.From(service)`).
- **State Pattern:** `AppointmentState` (soyut) → `PendingState`, `ApprovedState`, `RejectedState`, `CancelledState`, `CompletedState`. `Approve/Reject/Cancel/Complete` önce `State.Next(action)` ile geçişi doğrular (geçersizse `DomainException`), sonra durumu, işleyeni ve zamanı kaydeder; red/iptalde neden zorunlu; `Complete` randevu başlamadan çalışmaz. `HoldsSlot` = PENDING/APPROVED. `IsWithinCancellationDeadline` hastanın iptal süre sınırını hesaplar (yalnız onaylı randevu).
- **Olaylar:** `AppointmentRequestedEvent`, `AppointmentApprovedEvent`, `AppointmentRejectedEvent`, `AppointmentCancelledEvent` (`DomainEvent` kayıtları; kimlikler, hizmet adı, başlangıç, red/iptal nedeni ve iptal edeni taşır) `Raise` ile üretilir ve SaveChanges sırasında e-posta outbox'a çevrilir ([[bildirimler]]).
- **`EmailOutboxMessage`** (`Domain/Entities/Notifications`): gönderilecek e-posta kaydı. `MarkSent`, `RegisterFailure(error, now, RetryPolicy)` (sayaç artar; sınır aşılınca `FAILED`, aksi halde `NextAttemptAt = now + BaseDelay·2^(deneme-1)`, `MaxDelay` ile sınırlı); yalnız bekleyen mesaj işlem görür.
- **`SlotCalculator`** (`Domain/Scheduling`): saf fonksiyon; girdiler `SlotParameters` (şube saatleri, uygunluklar, izin ve dolu aralıklar `TimeRange`), çıktı UTC slot listesi. `LocalDayBounds` DST günlerinde 23/25 saatlik gün sınırlarını verir.

- Ortak doğrulama: `Domain/Common/Guard` (boş/uzunluk/Guid).
