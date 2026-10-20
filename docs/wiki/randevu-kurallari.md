# Randevu İş Kuralları

Kaynak: `plan.md` §7. Tablolar: [[veritabani]]. Kalıp: State ([[tasarim-kaliplari]]). Testler: [[test-stratejisi]].

## Slot üretimi (sabit süre)
Hizmet süresi sabit. Başlangıçlar ızgara adımıyla üretilir (`Appointment:SlotGridMinutes`, varsayılan 15). `GetAvailableSlots(doctorId, serviceId, date)`:
1. Doktor hizmeti **`APPROVED`** sunuyor mu?
2. O günün `DOCTOR_AVAILABILITY` pencereleri (geçerlilik içinde).
3. Hastane çalışma saatiyle **kesiştir**.
4. `DOCTOR_TIME_OFF` çıkar.
5. Pencere içinde adaylar: `[t, t+duration)` tamamen sığmalı.
6. `PENDING/APPROVED` randevularla çakışanları ele.
7. Geçmiş, `MinLeadTime` (60 dk) ve `MaxBookingHorizonDays` (60) kuralları.

## Oluşturma (`CreateAppointment`)
Sıra: hasta aktif → doktor/hizmet/hastane tutarlı → doktor hizmeti onaylı sunuyor → aralık üretilen bir slot → hastanın aynı aralıkta başka aktif randevusu yok → kayıt `PENDING` (+snapshot) → `AppointmentRequestedEvent`. **`PENDING` slotu rezerve eder.**

## Çakışma koruması (iki katman)
1. Uygulama ön kontrolü (anlaşılır hata).
2. **PostgreSQL exclusion constraint** (asıl güvence; yarış durumunu kapatır):
```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE appointment ADD CONSTRAINT ex_doctor_no_overlap
  EXCLUDE USING gist (doctor_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)
  WHERE (status IN ('PENDING','APPROVED'));
ALTER TABLE appointment ADD CONSTRAINT ex_patient_no_overlap
  EXCLUDE USING gist (patient_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)
  WHERE (status IN ('PENDING','APPROVED'));
```
İhlal `23P01` → `409 Conflict`. `[)` aralığı sayesinde bitişik randevular çakışmaz.

## Faz 1B bulguları (spike doğrulandı)
- İki kısıt da gerçek PostgreSQL 16'da çalışıyor: `ex_doctor_no_overlap` (doktor) ve `ex_patient_no_overlap` (hasta). Aynı hasta iki doktorda çakışan saatte randevu alamaz.
- Kısıtlar EF ile ifade edilemez; **migration içinde ham SQL** (`migrationBuilder.Sql`) olarak tanımlı. `btree_gist` ilk migration'da açılır.
- `AppDbContext.SaveChangesAsync`, `PostgresException` `23P01` (exclusion) ve `23505` (unique) hatalarını `DataConflictException(constraintName)` olarak fırlatır; `GlobalExceptionHandler` bunu **409 ProblemDetails**'e çevirir (HTTP seviyesinde test edildi).
- Doğrulanan davranışlar: birebir aynı aralık, başı/sonu/içinde/kapsayan kesişim çakışır; bitişik aralıklar çakışmaz (`[)`); farklı doktor aynı saat çakışmaz; `CANCELLED`/`REJECTED`/`COMPLETED` slotu serbest bırakır; `PENDING`/`APPROVED` slotu tutar; iptal sonrası alınan slot yine korunur.
- **20 paralel istek** aynı slota → tam 1 başarılı, 19 `DataConflictException(ex_doctor_no_overlap)`; tabloda 1 satır.
- CHECK kısıtları: `ck_appointment_range` (`end_at > start_at`, ham SQL ile atlatma denemesi de reddedilir), `ck_appointment_status`.
- Spike tablosu: `id, patient_id (FK patient), doctor_id (FK yok), start_at, end_at, status, created_at`. `service_id`, `hospital_id`, snapshot alanları, `decision_reason`, `row_version`, doktor/hizmet FK'leri Faz 3-4'te eklenir. Durum geçişleri (State Pattern) Faz 4; spike testlerinde durum doğrudan SQL ile değiştirilir.

## Durum makinesi
- `Pending → Approved` (doktor), `Pending → Rejected` (doktor), `Pending → Cancelled` (hasta/yönetici).
- `Approved → Cancelled` (hasta süre sınırı içinde / doktor / yönetici), `Approved → Completed` (doktor, randevu başladıktan sonra).
- `Rejected`, `Cancelled`, `Completed` terminal; slotu serbest bırakır.
- Yalnız ilgili randevunun doktoru onaylar/reddeder/tamamlar. Hasta yalnız kendi randevusunu iptal eder; `CancellationDeadlineHours` (2 sa) içindeki onaylı randevuyu iptal edemez (doktor/yönetici bu sınıra tabi değil). Red ve iptalde `decision_reason` zorunlu.
- Yetki ve sahiplik: [[kimlik-ve-yetki]]. Endpoint'ler: [[api-uc-noktalari]].

## Doktor-hizmet ataması (yönetici onaylı)
- Doktor kendi hastanesindeki aktif hizmeti **talep eder** → `PENDING`; yönetici onaylar (`APPROVED`) veya reddeder (`REJECTED`). *(Faz 3'te uygulandı: [[domain-modeli]], [[kararlar]] K-41; katalog yalnız APPROVED hizmetli doktoru gösterir.)*
- Yönetici doğrudan atarsa kayıt direkt `APPROVED`.
- Yalnız `APPROVED` slot üretir. Doktor ve hizmet aynı hastaneye ait olmalı.
- Gelecekte aktif randevusu olan hizmet doktordan kaldırılamaz. Reddedilen talep yeniden `PENDING` olabilir.
