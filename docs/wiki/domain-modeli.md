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

## Aggregate'ler (planlanan)
`User` (+alt tipler), `Organization`/`Hospital` (+çalışma saatleri, bölüm), `Service`, `Doctor` (+uygunluk, izin, hizmet), `Appointment` (+state). Kurallar: [[randevu-kurallari]].
