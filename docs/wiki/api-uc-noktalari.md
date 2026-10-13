# API Uç Noktaları

Kaynak: `plan.md` §10. Rol URL'de taşınmaz; kaynak odaklı rotalar + `me` kalıbı. Politikalar: [[kimlik-ve-yetki]]. Kurallar: [[randevu-kurallari]].

| Metot | Yol | Politika |
|---|---|---|
| POST | `/api/auth/register` | anonim (yalnız hasta) |
| POST | `/api/auth/login` | anonim |
| POST | `/api/auth/logout` | giriş |
| GET | `/api/auth/me`, `/api/auth/csrf` | giriş |
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
| POST | `/api/users/{id}/deactivate` | Admin + kapsam |
| GET/PUT | `/api/organization` | SuperAdmin |
| GET/POST | `/api/hospitals` (POST: SuperAdmin) | Admin |
| GET/PUT | `/api/hospitals/{id}`; PUT `/working-hours`; POST `/deactivate` | Admin + kapsam |
| GET/POST | `/api/hospitals/{id}/departments` | Admin + kapsam |
| PUT | `/api/departments/{id}` (yeniden adlandır) · POST `/api/departments/{id}/deactivate` | Admin + kapsam |
| GET/POST/PUT | `/api/services` (`?hospitalId=`), `/api/services/{id}`; POST `/{id}/deactivate` | Admin + kapsam |

**Gerçekleşen:** Faz 1 — `GET /api/auth/csrf`, `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`, `/health`. Faz 2 — `GET/PUT /api/organization`; `GET/POST /api/hospitals`, `GET/PUT /api/hospitals/{id}`, `PUT /{id}/working-hours`, `POST /{id}/deactivate`; `GET/POST /api/hospitals/{id}/departments`, `PUT /api/departments/{id}`, `POST /api/departments/{id}/deactivate`; `GET/POST /api/services`, `GET/PUT /api/services/{id}`, `POST /{id}/deactivate`; anonim `GET /api/catalog/hospitals`, `/hospitals/{id}/departments`, `/hospitals/{id}/services`. Diğerleri planlanmıştır.

Yanıt kodları (Faz 2): kapsam dışı şube/bölüm/hizmet → **404**; rol yetersiz → 403; aynı isim → 409; doğrulama (saat dilimi, süre, ücret, para birimi, çalışma saati) → 400 alan bazlı; aktif hizmeti olan bölümü pasifleştirme → 409. Oluşturma 201 `{ id }`, güncelleme/pasifleştirme 204. Çalışma saati: `Day` 0=Pazar…6=Cumartesi; `PUT working-hours` listeyle **eşitler** (listede olmayan gün kapanır; boş liste = hepsi kapalı); okumada Pazartesi başlı sıra.

Endpoint'ler Minimal API; her feature'ın Api klasöründe `IEndpoint` uygulayan sınıflarda ([[mimari]]).
