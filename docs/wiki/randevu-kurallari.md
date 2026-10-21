# Randevu İş Kuralları

Kaynak: `plan.md` §7. Tablolar: [[veritabani]]. Kalıp: State ([[tasarim-kaliplari]]). Testler: [[test-stratejisi]].

> **Faz 4'te uygulandı** (2026-10-21). Kod haritası: `Domain/Scheduling/SlotCalculator` (saf slot hesabı), `Domain/Entities/Appointments/` (`Appointment`, `States/`, olaylar), `Application/Features/Appointments/` (`SlotAvailabilityService`, komutlar, sorgular), `Persistence/Features/Appointments/`. Kararlar: [[kararlar]] K-49…K-56.

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

## Faz 4 uygulama notları
- **Ayarlar** (`Appointment` bölümü, `AppointmentSettings`): `SlotGridMinutes` 15, `MinLeadMinutes` 60, `MaxBookingHorizonDays` 60, `CancellationDeadlineHours` 2. Çalışma anında okunur (testler geçersiz kılabilir).
- **Izgara** gece yarısından hizalıdır (09:10 başlayan pencere ilk slotu 09:15 verir). Ufuk: şubenin yerel bugününden +60 gün; geçmiş gün boş döner. Yerel saat → UTC dönüşümü DST'ye duyarlı; ileri geçişte var olmayan yerel saatler atlanır.
- **`StartAt` slotla birebir eşleşmeli**; bitişi istemci göndermez, sunucu `başlangıç + hizmet süresi` olarak hesaplar ve hizmet adı/süre/ücret/para birimini randevuya **snapshot** olarak yazar (hizmet sonradan değişse de randevu sabit).
- `POST /api/appointments` hata kodları: `appointment.doctor_not_found` 404 (pasif doktor/şube dahil) · `appointment.service_invalid` 400 (başka şube/pasif/yok) · `appointment.service_not_offered` 400 (onaylı değil) · `appointment.slot_unavailable` 409 (üretilen slot değil/dolu/geçmiş) · `appointment.patient_overlap` 409 · `appointment.slot_taken` 409 (ön kontrolü geçen eşzamanlı yarışta kaybeden; DB `ex_doctor_no_overlap`).
- **Durum geçişleri:** `Allows` ile önceden denetlenir → `409 appointment.invalid_transition`; ayrıca `DomainException` ikinci savunmadır. Doktor onay/red/tamamlama yalnız **kendi** randevusunda (başkasınınki 404). Red ve iptalde neden zorunlu (400 alan bazlı). Tamamlama: onaylı **ve** başlamış (`409 appointment.not_started`).
- **İptal:** hasta (kendi), doktor (kendi), yönetici (kapsamdaki şube); personel 403. Hasta, onaylı randevuyu başlamasına **2 saatten az** kala iptal edemez (`409 appointment.cancellation_deadline_passed`; tam 2 saat kala hâlâ iptal edilebilir); bekleyen randevu her zaman iptal edilir; doktor/yönetici sınırsız.
- **Eşzamanlı durum geçişleri:** `appointment.xmin` (PostgreSQL sistem sütunu) iyimser eşzamanlılık belirtecidir; aynı randevuyu aynı anda iki istek değiştirirse kaybeden `409` alır (`DbUpdateConcurrencyException` → `DataConflictException`).
- **Gelecek randevu denetimleri (Faz 3'ten devreden):** gelecekte (bitişi şimdiden sonra) PENDING/APPROVED randevusu olan **hizmet** doktordan kaldırılamaz (`doctor.service_has_future_appointments`, hem `DELETE` hem `PUT services` çıkarma); aktif randevu olan aralığa **izin** eklenemez (`doctor.time_off_has_appointments`); aktif randevusu olan **doktor veya hasta** pasifleştirilemez (`user.has_future_appointments`). Uygunluk penceresi silme/güncelleme bilinçli olarak engellenmez: mevcut randevular geçerli kalır, yalnız yeni slot üretimi etkilenir.
- Domain olayları (`AppointmentRequested/Approved/Rejected/Cancelled`) aggregate'te üretilir; dağıtım ve outbox Faz 5'tedir.
- Liste uçları en fazla 200 kayıt döner (sayfalama Faz 6).

## Faz 1B bulguları (spike doğrulandı)
- İki kısıt da gerçek PostgreSQL 16'da çalışıyor: `ex_doctor_no_overlap` (doktor) ve `ex_patient_no_overlap` (hasta). Aynı hasta iki doktorda çakışan saatte randevu alamaz.
- Kısıtlar EF ile ifade edilemez; **migration içinde ham SQL** (`migrationBuilder.Sql`) olarak tanımlı. `btree_gist` ilk migration'da açılır.
- `AppDbContext.SaveChangesAsync`, `PostgresException` `23P01` (exclusion) ve `23505` (unique) hatalarını `DataConflictException(constraintName)` olarak fırlatır; `GlobalExceptionHandler` bunu **409 ProblemDetails**'e çevirir (HTTP seviyesinde test edildi).
- Doğrulanan davranışlar: birebir aynı aralık, başı/sonu/içinde/kapsayan kesişim çakışır; bitişik aralıklar çakışmaz (`[)`); farklı doktor aynı saat çakışmaz; `CANCELLED`/`REJECTED`/`COMPLETED` slotu serbest bırakır; `PENDING`/`APPROVED` slotu tutar; iptal sonrası alınan slot yine korunur.
- **20 paralel istek** aynı slota → tam 1 başarılı, 19 `DataConflictException(ex_doctor_no_overlap)`; tabloda 1 satır.
- CHECK kısıtları: `ck_appointment_range` (`end_at > start_at`, ham SQL ile atlatma denemesi de reddedilir), `ck_appointment_status`.
- Spike tablosu: `id, patient_id (FK patient), doctor_id (FK yok), start_at, end_at, status, created_at`. **Faz 4'te (migration `AppointmentCore`) tablo genişledi:** `hospital_id`, `service_id` FK'leri, `doctor_id` FK'si, snapshot alanları, `patient_note`, `decision_reason`, `status_changed_by`, `updated_at`, `xmin`. Migration, FK'lerle uyumsuz spike satırlarını siler. Spike testleri gerçek doktor/hizmet/şube kaydı yazacak biçimde uyarlandı; HTTP yarış testi artık gerçek `POST /api/appointments` üzerindedir (`AppointmentConcurrencyTests`).

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
