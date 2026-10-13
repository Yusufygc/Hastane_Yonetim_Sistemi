# Veritabanı

Kaynak: `plan.md` §6 (tam Mermaid ER orada). PostgreSQL 16, tek `AppDbContext`, tüm ID'ler `uuid` (Guid v7). Ortam: [[docker-ortam]].

## Tablolar
- **Kurum:** `ORGANIZATION` → `HOSPITAL` (`time_zone` IANA) → `HOSPITAL_WORKING_HOURS` (gün bazlı; kayıt yoksa kapalı), `DEPARTMENT`, `SERVICE` (`duration_minutes`, `price numeric(12,2)`, `currency`).
- **Kullanıcı (TPT):** `USER_ACCOUNT` (email UK, password_hash, role, is_active, `security_stamp`, `failed_login_count`, `lockout_end`) + `PATIENT`, `DOCTOR`, `STAFF`, `ADMIN` (PK = `USER_ACCOUNT.id`). Bkz. [[domain-modeli]].
- **Doktor:** `DOCTOR_SERVICE` (PK doctor+service, `status` PENDING/APPROVED/REJECTED, requested_by, decided_by), `DOCTOR_AVAILABILITY` (gün, saat aralığı, geçerlilik), `DOCTOR_TIME_OFF`.
- **Randevu (Faz 1B'de yalnız çekirdek sütunlar mevcut, bkz. [[randevu-kurallari]]):** `APPOINTMENT` (patient, doctor, service, hospital, `start_at/end_at` UTC, `status`, snapshot alanları, `decision_reason`, `row_version`=xmin).
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
- **Çakışma:** exclusion constraint — bkz. [[randevu-kurallari]].

## İndeksler
`APPOINTMENT (doctor_id, start_at)`, `(patient_id, start_at DESC)`, `(hospital_id, status, start_at)`; kısmi: `EMAIL_OUTBOX WHERE status='PENDING'`, `DOCTOR_SERVICE WHERE status='PENDING'`.
