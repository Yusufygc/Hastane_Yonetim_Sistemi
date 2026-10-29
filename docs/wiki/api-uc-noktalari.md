# API Uç Noktaları

Kaynak: `plan.md` §10. Rol URL'de taşınmaz; kaynak odaklı rotalar + `me` kalıbı. Politikalar: [[kimlik-ve-yetki]]. Kurallar: [[randevu-kurallari]].

**Ortak davranışlar (Faz 6):** her yanıtta `X-Correlation-Id` (hata `traceId`'siyle aynı); liste uçları `?page=1&pageSize=50` (1–200) alır ve gövde dizi kalır, toplam `X-Total-Count`, sayfa `X-Page`, boyut `X-Page-Size` başlıklarındadır ([[kararlar]] K-62, K-63). Makine okunur sözleşme: `docs/api/openapi.v1.json` ([[operasyon]]).

| Metot | Yol | Politika |
|---|---|---|
| POST | `/api/auth/register` | anonim (yalnız hasta) |
| POST | `/api/auth/login` | anonim |
| POST | `/api/auth/logout` (gövde: refreshToken) | giriş |
| POST | `/api/auth/refresh` (gövde: refreshToken) | anonim (belirteçle) |
| POST | `/api/auth/forgot-password` (gövde: email) → **202** (hesap var/yok aynı yanıt) | anonim (hız sınırlı) |
| POST | `/api/auth/reset-password` (gövde: token, newPassword) → 204; geçersiz/kullanılmış/süresi dolmuş belirteç 400 `auth.invalid_reset_token` | anonim (hız sınırlı) |
| POST | `/api/auth/change-password` (gövde: currentPassword, newPassword) | giriş |
| GET | `/api/auth/me` | giriş |
| GET | `/api/catalog/hospitals`, `/hospitals/{id}/departments`, `/hospitals/{id}/services` | anonim |
| GET | `/api/catalog/doctors?hospitalId=&serviceId=` | anonim |
| GET | `/api/catalog/doctors/{id}/slots?serviceId=&date=` | anonim |
| GET/PUT | `/api/patients/me` | Patient |
| GET | `/api/patients/me/appointments` | Patient |
| GET/PUT | `/api/doctors/me` | Doctor |
| GET | `/api/doctors/me/appointments`, `/schedule` | Doctor |
| GET/POST | `/api/doctors/me/availability`; PUT/DELETE `/{id}` | Doctor |
| GET/POST | `/api/doctors/me/time-off`; DELETE `/{id}` | Doctor |
| GET/POST | `/api/doctors/me/services` (talep); DELETE `/{serviceId}` | Doctor |
| POST | `/api/appointments` | Patient |
| GET | `/api/appointments/{id}` | giriş + sahiplik |
| POST | `/api/appointments/{id}/approve` · `/reject` · `/complete` | Doctor + sahiplik |
| POST | `/api/appointments/{id}/cancel` | Patient/Doctor/Admin + sahiplik |
| GET | `/api/appointments?hospitalId=&status=` | Admin + kapsam |
| GET/POST | `/api/doctors`, `/api/staff`, `/api/admins` | Admin + kapsam (admin oluşturma: SuperAdmin) |
| GET/PUT | `/api/doctors/{id}` | Admin + kapsam |
| PUT | `/api/doctors/{id}/services` (doğrudan atama) | Admin + kapsam |
| GET | `/api/doctor-service-requests?status=pending` | Admin + kapsam |
| POST | `/api/doctors/{doctorId}/services/{serviceId}/approve` · `/reject` | Admin + kapsam |
| DELETE | `/api/doctors/{doctorId}/services/{serviceId}` | Admin + kapsam |
| POST | `/api/users/{id}/deactivate` · `/activate` | Admin + kapsam |
| GET/PUT | `/api/organization` | SuperAdmin |
| GET/POST | `/api/hospitals` (POST: SuperAdmin) | Admin |
| GET/PUT | `/api/hospitals/{id}`; PUT `/working-hours`; POST `/deactivate` | Admin + kapsam |
| GET/POST | `/api/hospitals/{id}/departments` | Admin + kapsam |
| PUT | `/api/departments/{id}` (yeniden adlandır) · POST `/api/departments/{id}/deactivate` | Admin + kapsam |
| GET/POST/PUT | `/api/services` (`?hospitalId=`), `/api/services/{id}`; POST `/{id}/deactivate` | Admin + kapsam |

**Gerçekleşen:** Faz 1 — `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`, `/health` (CSRF ucu JWT'ye geçişle kaldırıldı). Faz 2 — `GET/PUT /api/organization`; `GET/POST /api/hospitals`, `GET/PUT /api/hospitals/{id}`, `PUT /{id}/working-hours`, `POST /{id}/deactivate`; `GET/POST /api/hospitals/{id}/departments`, `PUT /api/departments/{id}`, `POST /api/departments/{id}/deactivate`; `GET/POST /api/services`, `GET/PUT /api/services/{id}`, `POST /{id}/deactivate`; anonim `GET /api/catalog/hospitals`, `/hospitals/{id}/departments`, `/hospitals/{id}/services`. Faz 3 — `GET/PUT /api/patients/me`; `GET/PUT /api/doctors/me`, `GET/POST /api/doctors/me/availability` (+ `PUT/DELETE /{id}`), `GET/POST /api/doctors/me/time-off` (+ `DELETE /{id}`), `GET/POST /api/doctors/me/services` (+ `DELETE /{serviceId}`); `GET/POST /api/doctors`, `GET/PUT /api/doctors/{id}`, `PUT /api/doctors/{id}/services`, `POST /api/doctors/{doctorId}/services/{serviceId}/approve|reject`, `DELETE /api/doctors/{doctorId}/services/{serviceId}`; `GET /api/doctor-service-requests?status=`; `GET/POST /api/staff`; `GET/POST /api/admins` (POST: SuperAdmin); `POST /api/users/{id}/deactivate|activate`; `POST /api/auth/change-password`; anonim `GET /api/catalog/doctors?hospitalId=&serviceId=`. Faz 4 — anonim `GET /api/catalog/doctors/{id}/slots?serviceId=&date=yyyy-MM-dd`; `POST /api/appointments` (Patient), `GET /api/appointments/{id}` (giriş + sahiplik/kapsam), `POST /api/appointments/{id}/approve|reject|complete` (Doctor), `POST /api/appointments/{id}/cancel` (hasta/doktor/yönetici), `GET /api/appointments?hospitalId=&status=&from=&to=` (Admin + kapsam), `GET /api/patients/me/appointments?status=`, `GET /api/doctors/me/appointments?from=&to=&status=`, `GET /api/doctors/me/schedule?from=&to=` (onaylılar; varsayılan şimdiden 7 gün). Bu aşamada tüm planlanan uçlar gerçekleşmiştir.

Yanıt kodları (Faz 2): kapsam dışı şube/bölüm/hizmet → **404**; rol yetersiz → 403; aynı isim → 409; doğrulama (saat dilimi, süre, ücret, para birimi, çalışma saati) → 400 alan bazlı; aktif hizmeti olan bölümü pasifleştirme → 409. Oluşturma 201 `{ id }`, güncelleme/pasifleştirme 204. Çalışma saati: `Day` 0=Pazar…6=Cumartesi; `PUT working-hours` listeyle **eşitler** (listede olmayan gün kapanır; boş liste = hepsi kapalı); okumada Pazartesi başlı sıra.

Yanıt kodları (Faz 3): hasta/doktor yönetici uçlarında 403, anonim 401; kapsam dışı doktor/personel/şube/kullanıcı → **404**; başka doktorun uygunluk/izin kaydı → 404; aynı e-posta (`auth.email_taken`) / diploma (`doctor.diploma_taken`) → 409; aynı hizmet için bekleyen/onaylı talep tekrarı → 409 (`doctor.service_already_requested` / `doctor.service_already_approved`); bekleyen olmayan talebi reddetme → 409 (`doctor.service_not_pending`); onaylı hizmeti doktorun kaldırması → 409 (`doctor.service_approved_locked`); kendi hesabını pasifleştirme → 409 (`user.cannot_modify_self`); uygunluk çakışması → 409 (`doctor.availability_overlap`), izin çakışması → 409 (`doctor.time_off_overlap`); şube saatleri dışı uygunluk → 400 (`doctor.availability_outside_hours`); geçmişte biten izin → 400 (`doctor.time_off_in_past`); başka şubenin/pasif hizmeti talep/atama → 400 (`doctor.service_invalid`); yanlış mevcut parola / aynı parola → 400 alan bazlı (`auth.current_password_invalid`, `auth.password_unchanged`). Hizmet talebi `202 Accepted`, oluşturma 201 `{ id }`, güncelleme/silme/onay/red 204. Hasta profili yanıtında T.C. **maskeli** (`maskedIdentityNumber`, ör. `100******46`). Durum alanları büyük harfli metin (`PENDING`/`APPROVED`/`REJECTED`); `day` 0=Pazar…6=Cumartesi; `startTime`/`endTime` `HH:mm`; `validFrom`/`validTo` `yyyy-MM-dd`; izin zamanları ISO 8601.

Yanıt kodları (Faz 4): başkasının randevusu ve olmayan randevu → **404**; kapsam dışı şube yöneticisi → 404; doktor yalnız kendi randevusunda işlem yapar (başkasınınki 404); hasta onay/red/tamamlama ucuna 403; personel randevu ucuna 403; slot olmayan saat / dolu slot → 409 `appointment.slot_unavailable`; çakışan hasta randevusu → 409 `appointment.patient_overlap`; eşzamanlı yarışta kaybeden → 409 `appointment.slot_taken`; geçersiz durum geçişi → 409 `appointment.invalid_transition`; başlamamış randevuyu tamamlama → 409 `appointment.not_started`; hastanın iptal süre sınırı → 409 `appointment.cancellation_deadline_passed`; red/iptal nedeni yok → 400 (`errors.reason`); onaysız/başka şube hizmeti → 400; `date` yalnız `yyyy-MM-dd` (diğer biçimler 400 `errors.date`). Randevu zamanları UTC; slot yanıtında ayrıca şubenin yerel saati (`localStart`) ve `timeZoneId` bulunur. Liste uçları en fazla 200 kayıt döner.

Çalıştırılabilir örnekler ve smoke test: [[api-ornekleri]].

Endpoint'ler Minimal API; her feature'ın Api klasöründe `IEndpoint` uygulayan sınıflarda ([[mimari]]).
