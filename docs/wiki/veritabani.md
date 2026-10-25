# Veritabanı

Kaynak: `plan.md` §6 (tam Mermaid ER orada). PostgreSQL 16, tek `AppDbContext`, tüm ID'ler `uuid` (Guid v7). Ortam: [[docker-ortam]].

## Tablolar
- **Kurum (Faz 2'de eklendi):** `ORGANIZATION` → `HOSPITAL` (`time_zone` IANA; domain sınıfı `HospitalBranch`) → `HOSPITAL_WORKING_HOURS` (gün bazlı; kayıt yoksa kapalı; `(hospital_id, day_of_week)` benzersiz; `close_time > open_time`), `DEPARTMENT` (**`is_active` eklendi**, `(hospital_id, name)` benzersiz), `SERVICE` (`duration_minutes` 1–480, `price numeric(12,2) >= 0`, `currency char(3)`, bölüm opsiyonel, `(hospital_id, name)` benzersiz). İsim benzersizliği uygulamada büyük/küçük harf duyarsız denetlenir, DB'de ek olarak benzersiz indeks vardır. FK'ler `ON DELETE RESTRICT`; `hospital_working_hours` şubeyle birlikte silinir (cascade).
- **Kullanıcı (TPT):** `USER_ACCOUNT` (email UK, password_hash, role, is_active, `security_stamp`, `failed_login_count`, `lockout_end`) + `PATIENT`, `ADMIN` (Faz 2: `organization_id` FK, `hospital_id` FK nullable, `access_level`, `ck_admin_scope`: süper yönetici ⇔ şube boş), `DOCTOR`, `STAFF` (Faz 3; PK = `USER_ACCOUNT.id`). `DOCTOR`: `department_id` FK, `diploma_no` benzersiz. `STAFF`: `hospital_id` FK, `department_id` FK nullable. Bkz. [[domain-modeli]].
- **Doktor (Faz 3):** `DOCTOR_SERVICE` (PK doctor+service, `status` CHECK PENDING/APPROVED/REJECTED, `requested_by`/`decided_by` → `user_account` FK, `created_at`, `decided_at`, `(status, created_at)` indeksi), `DOCTOR_AVAILABILITY` (`day_of_week` 0-6, `start_time`/`end_time` `time`, `valid_from` date, `valid_to` date nullable; CHECK `end_time > start_time` ve `valid_to IS NULL OR valid_to >= valid_from`), `DOCTOR_TIME_OFF` (`start_at`/`end_at` timestamptz, CHECK `end_at > start_at`). Doktor silinirse alt kayıtları cascade silinir (pratikte doktor silinmez, pasifleştirilir).
- **Randevu (Faz 4, migration `AppointmentCore`; bkz. [[randevu-kurallari]]):** `APPOINTMENT` (`hospital_id`, `patient_id`, `doctor_id`, `service_id` FK'leri `RESTRICT`; `start_at/end_at` UTC; `status` CHECK; `service_name_snapshot`, `duration_minutes_snapshot > 0`, `price_snapshot >= 0`, `currency_snapshot`; `patient_note`, `decision_reason` ≤ 500; `status_changed_by` → `user_account` FK; `created_at/updated_at`; **`xmin` iyimser eşzamanlılık belirteci** (PostgreSQL sistem sütunu, EF `IsConcurrencyToken`). İndeksler: `(doctor_id, start_at)`, `(patient_id, start_at DESC)`, `(hospital_id, status, start_at)`. Çakışma kısıtları `ex_doctor_no_overlap`/`ex_patient_no_overlap` Faz 1B'den.
- **Oturum (JWT):** `REFRESH_TOKEN` (`user_account_id` FK cascade, `token_hash` benzersiz SHA-256, `expires_at`, `revoked_at`, `replaced_by_token_id`). Düz belirteç saklanmaz.
- **Bildirim (Faz 5, migration `EmailOutbox`):** `EMAIL_OUTBOX` (`dedup_key` benzersiz = olay kimliği + alıcı, `to_address`, `to_name`, `subject`, `body`, `template_key`, `status` CHECK PENDING/SENT/FAILED, `attempt_count`, `last_error`, `created_at`, `next_attempt_at`, `sent_at`). Kısmi indeks `ix_email_outbox_pending (next_attempt_at, created_at) WHERE status='PENDING'`. Ayrıntı: [[bildirimler]]; kalıp: [[tasarim-kaliplari]].

## Çok şubeli / tek şubeli
`ORGANIZATION` her zaman var. Tek şube = 1 organizasyon + 1 hastane (seed). Kod yolu aynı. Kapsam filtresi: [[kimlik-ve-yetki]].

## Bütünlük kuralları
- **Zaman:** `timestamptz` UTC; haftalık kurallar hastanenin saat diliminde yorumlanıp UTC'ye çevrilir (DST güvenli).
- **Para:** `numeric(12,2)` + para birimi; float yok.
- **Snapshot:** randevu anında hizmet adı/süre/ücret kopyalanır.
- **Silme:** randevusu olan kayıtlar fiziksel silinmez (`ON DELETE RESTRICT`), `is_active=false`.
- **CHECK:** `end > start`, `duration_minutes > 0`, `price >= 0`, `day_of_week 0-6`, status değer listesi, `(access_level='SUPER_ADMIN') = (hospital_id IS NULL)`.
- **Tekillik:** email, identity_number, diploma_no; `(hospital_id, day_of_week)`.
- **Hassas veri (KVKK):** TC maskeli döner, loglanmaz, mümkünse şifreli + arama hash'i.
- **Çakışma:** exclusion constraint — bkz. [[randevu-kurallari]]. Faz 3'te ek olarak (migration `DoctorsAndStaff`, ham SQL): `ex_doctor_availability_no_overlap` = `EXCLUDE USING gist (doctor_id WITH =, day_of_week WITH =, daterange(valid_from, valid_to, '[]') WITH &&, tsrange(DATE '2000-01-01' + start_time, DATE '2000-01-01' + end_time, '[)') WITH &&)` (valid_to NULL = sınırsız); `ex_doctor_time_off_no_overlap` = `(doctor_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)`. Uygulama ön denetimi yapar, eşzamanlı yarışta veritabanı 23P01 ile engeller (→ 409, `DataConflictException`).
- `appointment.doctor_id` FK'si Faz 4'te eklendi (Faz 3'te belirtilen boşluk kapandı; [[kararlar]] K-55).

## İndeksler
`APPOINTMENT (doctor_id, start_at)`, `(patient_id, start_at DESC)`, `(hospital_id, status, start_at)`; kısmi: `EMAIL_OUTBOX WHERE status='PENDING'`, `DOCTOR_SERVICE WHERE status='PENDING'`, `APPOINTMENT (created_at) WHERE status='PENDING'` (zaman aşımı taraması). Faz 6 gözden geçirmesi ve plan testleri: [[operasyon]] (K-66).

## T.C. kimlik no şifreleme (Faz 6, KVKK)
`PATIENT.identity_number` düz metin değil: `enc:v1:` önekli **AES-256-GCM** şifreli metindir (`varchar(100)`; migration `Hardening`, geri alınmamalı). Eşitlik araması ve benzersizlik için aynı satırda `identity_number_hash` (HMAC-SHA256 **kör indeks**, benzersiz indeks `ix_patient_identity_number_hash`) tutulur; uygulama katmanı şifreler/çözer (EF value converter, `AesGcmSensitiveDataProtector`), veritabanı yalnız şifreli değeri görür. Ana anahtar `Security:DataProtection:Key`. Eski düz metin satırlar açılışta `IdentityNumberUpgrader` ile yükseltilir. Ayrıntı ve anahtar yönetimi: [[operasyon]], karar [[kararlar]] K-65.
