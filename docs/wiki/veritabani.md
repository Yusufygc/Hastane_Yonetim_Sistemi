# Veritabanı

Kaynak: `plan.md` §6 (tam Mermaid ER orada). PostgreSQL 16, tek `AppDbContext`, tüm ID'ler `uuid` (Guid v7). Ortam: [[docker-ortam]].

## Tablolar
- **Kurum (Faz 2'de eklendi):** `ORGANIZATION` → `HOSPITAL` (`time_zone` IANA; domain sınıfı `HospitalBranch`) → `HOSPITAL_WORKING_HOURS` (gün bazlı; kayıt yoksa kapalı; `(hospital_id, day_of_week)` benzersiz; `close_time > open_time`), `DEPARTMENT` (**`is_active` eklendi**, `(hospital_id, name)` benzersiz), `SERVICE` (`duration_minutes` 1–480, `price numeric(12,2) >= 0`, `currency char(3)`, bölüm opsiyonel, `(hospital_id, name)` benzersiz). İsim benzersizliği uygulamada büyük/küçük harf duyarsız denetlenir, DB'de ek olarak benzersiz indeks vardır. FK'ler `ON DELETE RESTRICT`; `hospital_working_hours` şubeyle birlikte silinir (cascade).
- **Kullanıcı (TPT):** `USER_ACCOUNT` (email UK, password_hash, role, is_active, `security_stamp`, `failed_login_count`, `lockout_end`) + `PATIENT`, `ADMIN` (Faz 2: `organization_id` FK, `hospital_id` FK nullable, `access_level`, `ck_admin_scope`: süper yönetici ⇔ şube boş), `DOCTOR`, `STAFF` (Faz 3; PK = `USER_ACCOUNT.id`). `DOCTOR`: `department_id` FK, `diploma_no` benzersiz. `STAFF`: `hospital_id` FK, `department_id` FK nullable. Bkz. [[domain-modeli]].
- **Doktor (Faz 3):** `DOCTOR_SERVICE` (PK doctor+service, `status` CHECK PENDING/APPROVED/REJECTED, `requested_by`/`decided_by` → `user_account` FK, `created_at`, `decided_at`, `(status, created_at)` indeksi), `DOCTOR_AVAILABILITY` (`day_of_week` 0-6, `start_time`/`end_time` `time`, `valid_from` date, `valid_to` date nullable; CHECK `end_time > start_time` ve `valid_to IS NULL OR valid_to >= valid_from`), `DOCTOR_TIME_OFF` (`start_at`/`end_at` timestamptz, CHECK `end_at > start_at`). Doktor silinirse alt kayıtları cascade silinir (pratikte doktor silinmez, pasifleştirilir).
- **Randevu (Faz 1B'de yalnız çekirdek sütunlar mevcut, bkz. [[randevu-kurallari]]):** `APPOINTMENT` (patient, doctor, service, hospital, `start_at/end_at` UTC, `status`, snapshot alanları, `decision_reason`, `row_version`=xmin).
- **Oturum (JWT):** `REFRESH_TOKEN` (`user_account_id` FK cascade, `token_hash` benzersiz SHA-256, `expires_at`, `revoked_at`, `replaced_by_token_id`). Düz belirteç saklanmaz.
- **Bildirim:** `EMAIL_OUTBOX` (dedup_key UK, status, attempt_count). Bkz. [[tasarim-kaliplari]].

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
- **Bilinen boşluk:** `appointment.doctor_id` FK'si hâlâ yok (mevcut test verileri rastgele doktor kimliği kullanıyor); randevu tablosu genişletilirken Faz 4'te eklenecek ([[kararlar]] K-31).

## İndeksler
`APPOINTMENT (doctor_id, start_at)`, `(patient_id, start_at DESC)`, `(hospital_id, status, start_at)`; kısmi: `EMAIL_OUTBOX WHERE status='PENDING'`, `DOCTOR_SERVICE WHERE status='PENDING'`.
