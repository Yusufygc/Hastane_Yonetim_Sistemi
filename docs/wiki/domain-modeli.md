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
Gerçekleşen: `User` → `Patient`, `Admin` · `Organization` · **`HospitalBranch`** (+`HospitalWorkingHours` çocuk varlıkları) · `Department` · `Service` · `Appointment` (Faz 1B çekirdeği). Planlanan: `Doctor`, `Staff` (+uygunluk, izin, hizmet), `Appointment` State Pattern. Kurallar: [[randevu-kurallari]].

## Kurum varlıkları (Faz 2)
- **`HospitalBranch`** = ER'deki HOSPITAL (tablo `hospital`, API `/hospitals`). Sınıf adı kök namespace `Hospital` ile çakıştığı için bu ad kullanıldı ([[kararlar]] K-32). `SetWorkingHours` listeyi **eşitler** (mevcut gün güncellenir, yeni gün eklenir, listede olmayan kaldırılır; önce doğrular, sonra uygular → atomik). `TimeZoneId` IANA kimliği olarak doğrulanır.
- **`Service`**: sabit süre (1–480 dk), `numeric(12,2)` ücret (negatif olamaz), 3 harfli para birimi (büyük harfe çevrilir). Bölüm opsiyoneldir; verilirse aynı şubeye ait ve aktif olmalıdır (handler doğrular).
- **`Department`/`Service`/`HospitalBranch`** silinmez, `Deactivate()` ile pasifleştirilir. Aktif hizmeti olan bölüm pasifleştirilemez.
- **`Admin`**: `AdminAccessLevel` (`SuperAdmin` = organizasyon kapsamı, `HospitalId` boş; `BranchManager` = tek şube). `ScopeClaims` çok biçimli olarak `organization_id`, `access_level`, (`hospital_id`) üretir; cookie biletine yazılır ([[kimlik-ve-yetki]]).
- Ortak doğrulama: `Domain/Common/Guard` (boş/uzunluk/Guid).
