import { request, type ApiResponse, type Schemas } from './http'

export type Hospital = Schemas['CatalogHospitalDto']
export type Service = Schemas['CatalogServiceDto']
export type Doctor = Schemas['CatalogDoctorDto']
export type Slots = Schemas['AvailableSlotsDto']
export type Appointment = Schemas['AppointmentDto']
export type AuthTokens = Schemas['AuthTokens']
export type RegisterBody = Schemas['RegisterRequest']

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
}
