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

## Durum makinesi
- `Pending → Approved` (doktor), `Pending → Rejected` (doktor), `Pending → Cancelled` (hasta/yönetici).
- `Approved → Cancelled` (hasta süre sınırı içinde / doktor / yönetici), `Approved → Completed` (doktor, randevu başladıktan sonra).
- `Rejected`, `Cancelled`, `Completed` terminal; slotu serbest bırakır.
- Yalnız ilgili randevunun doktoru onaylar/reddeder/tamamlar. Hasta yalnız kendi randevusunu iptal eder; `CancellationDeadlineHours` (2 sa) içindeki onaylı randevuyu iptal edemez (doktor/yönetici bu sınıra tabi değil). Red ve iptalde `decision_reason` zorunlu.
- Yetki ve sahiplik: [[kimlik-ve-yetki]]. Endpoint'ler: [[api-uc-noktalari]].

## Doktor-hizmet ataması (yönetici onaylı)
- Doktor kendi hastanesindeki aktif hizmeti **talep eder** → `PENDING`; yönetici onaylar (`APPROVED`) veya reddeder (`REJECTED`).
- Yönetici doğrudan atarsa kayıt direkt `APPROVED`.
- Yalnız `APPROVED` slot üretir. Doktor ve hizmet aynı hastaneye ait olmalı.
- Gelecekte aktif randevusu olan hizmet doktordan kaldırılamaz. Reddedilen talep yeniden `PENDING` olabilir.
