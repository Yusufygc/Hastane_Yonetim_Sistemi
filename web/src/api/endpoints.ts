import { request, type ApiResponse, type Schemas } from './http'

export type Hospital = Schemas['CatalogHospitalDto']
export type Service = Schemas['CatalogServiceDto']
export type Doctor = Schemas['CatalogDoctorDto']
export type Slots = Schemas['AvailableSlotsDto']
export type Appointment = Schemas['AppointmentDto']
export type AuthTokens = Schemas['AuthTokens']
export type RegisterBody = Schemas['RegisterRequest']
export type DoctorProfile = Schemas['DoctorDetailDto']
export type DoctorService = Schemas['DoctorServiceDto']
export type Availability = Schemas['DoctorAvailabilityDto']
export type AvailabilityBody = Schemas['AvailabilityRequest']
export type TimeOff = Schemas['DoctorTimeOffDto']
export type TimeOffBody = Schemas['AddTimeOffRequest']

/** Randevu durumları (API büyük harfle döner). */
export type AppointmentStatus = 'PENDING' | 'APPROVED' | 'REJECTED' | 'CANCELLED' | 'COMPLETED'

export const auth = {
  login: (email: string, password: string) => request<AuthTokens>('POST', '/api/auth/login', { body: { email, password }, auth: false }),
  register: (body: RegisterBody) => request<Schemas['UserCreatedResponse']>('POST', '/api/auth/register', { body, auth: false }),
  logout: (refreshToken: string) => request<void>('POST', '/api/auth/logout', { body: { refreshToken } }),
}

export const catalog = {
  hospitals: (signal?: AbortSignal) => request<Hospital[]>('GET', '/api/catalog/hospitals', { auth: false, signal }),
  services: (hospitalId: string, signal?: AbortSignal) => request<Service[]>('GET', `/api/catalog/hospitals/${hospitalId}/services`, { auth: false, signal }),
  doctors: (hospitalId: string, serviceId: string, signal?: AbortSignal) =>
    request<Doctor[]>('GET', '/api/catalog/doctors', { query: { hospitalId, serviceId }, auth: false, signal }),
  /** @param date yyyy-MM-dd (API başka biçimi reddeder) */
  slots: (doctorId: string, serviceId: string, date: string, signal?: AbortSignal) =>
    request<Slots>('GET', `/api/catalog/doctors/${doctorId}/slots`, { query: { serviceId, date }, auth: false, signal }),
}

export const appointments = {
  create: (doctorId: string, serviceId: string, startAt: string, patientNote: string | null) =>
    request<Schemas['CreatedResponse']>('POST', '/api/appointments', { body: { doctorId, serviceId, startAt, patientNote } }),
  mine: (query: { status?: AppointmentStatus; page?: number; pageSize?: number }, signal?: AbortSignal): Promise<ApiResponse<Appointment[]>> =>
    request<Appointment[]>('GET', '/api/patients/me/appointments', { query, signal }),
  cancel: (id: string, reason: string) => request<void>('POST', `/api/appointments/${id}/cancel`, { body: { reason } }),
  // Doktor kararları (yalnız kendi randevusu; başkasınınki 404)
  approve: (id: string) => request<void>('POST', `/api/appointments/${id}/approve`),
  reject: (id: string, reason: string) => request<void>('POST', `/api/appointments/${id}/reject`, { body: { reason } }),
  complete: (id: string) => request<void>('POST', `/api/appointments/${id}/complete`),
}

/** Doktorun kendi kaynakları (`/api/doctors/me/*`, Doctor politikası). */
export const doctor = {
  profile: (signal?: AbortSignal) => request<DoctorProfile>('GET', '/api/doctors/me', { signal }),
  updateProfile: (specialty: string, phone: string | null) => request<void>('PUT', '/api/doctors/me', { body: { specialty, phone } }),
  appointments: (query: { status?: AppointmentStatus; from?: string; to?: string; page?: number; pageSize?: number }, signal?: AbortSignal) =>
    request<Appointment[]>('GET', '/api/doctors/me/appointments', { query, signal }),
  /** Onaylı randevu takvimi; varsayılan aralık sunucuda (şimdiden 7 gün). */
  schedule: (query: { from?: string; to?: string }, signal?: AbortSignal) => request<Appointment[]>('GET', '/api/doctors/me/schedule', { query, signal }),
  services: (signal?: AbortSignal) => request<DoctorService[]>('GET', '/api/doctors/me/services', { signal }),
  requestService: (serviceId: string) => request<void>('POST', '/api/doctors/me/services', { body: { serviceId } }),
  removeService: (serviceId: string) => request<void>('DELETE', `/api/doctors/me/services/${serviceId}`),
  availability: (signal?: AbortSignal) => request<Availability[]>('GET', '/api/doctors/me/availability', { signal }),
  addAvailability: (body: AvailabilityBody) => request<Schemas['CreatedResponse']>('POST', '/api/doctors/me/availability', { body }),
  removeAvailability: (id: string) => request<void>('DELETE', `/api/doctors/me/availability/${id}`),
  timeOff: (signal?: AbortSignal) => request<TimeOff[]>('GET', '/api/doctors/me/time-off', { signal }),
  addTimeOff: (body: TimeOffBody) => request<Schemas['CreatedResponse']>('POST', '/api/doctors/me/time-off', { body }),
  removeTimeOff: (id: string) => request<void>('DELETE', `/api/doctors/me/time-off/${id}`),
}
