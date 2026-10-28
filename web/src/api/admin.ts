import type { ApiResponse, Schemas } from './http'
import { request } from './http'
import type { Appointment, AppointmentStatus } from './endpoints'

export type AdminHospital = Schemas['HospitalDto']
export type HospitalDetail = Schemas['HospitalDetailDto']
export type HospitalBody = Schemas['UpdateHospitalRequest']
export type WorkingHoursItem = Schemas['WorkingHoursRequestItem']
export type Department = Schemas['DepartmentDto']
export type AdminService = Schemas['ServiceDto']
export type ServiceBody = Schemas['UpdateServiceRequest']
export type CreateServiceBody = Schemas['CreateServiceRequest']
export type AdminDoctor = Schemas['DoctorDto']
export type CreateDoctorBody = Schemas['CreateDoctorRequest']
export type UpdateDoctorBody = Schemas['UpdateDoctorRequest']
export type Staff = Schemas['StaffDto']
export type CreateStaffBody = Schemas['CreateStaffRequest']
export type ServiceRequest = Schemas['DoctorServiceRequestDto']
export type Organization = Schemas['OrganizationDto']

export type ServiceRequestStatus = 'PENDING' | 'APPROVED' | 'REJECTED'

type Page = { page?: number; pageSize?: number }

/**
 * Yönetici uçları (`Admin` politikası). Şube yöneticisi yalnızca kendi şubesini görür; kapsam dışı kaynak 404 döner.
 * Şube/kurum oluşturma ve kurum güncelleme yalnız SuperAdmin içindir (diğerleri 403).
 */
export const admin = {
  organization: (signal?: AbortSignal) => request<Organization>('GET', '/api/organization', { signal }),
  updateOrganization: (name: string) => request<void>('PUT', '/api/organization', { body: { name } }),

  hospitals: (signal?: AbortSignal) => request<AdminHospital[]>('GET', '/api/hospitals', { signal }),
  hospital: (id: string, signal?: AbortSignal) => request<HospitalDetail>('GET', `/api/hospitals/${id}`, { signal }),
  createHospital: (body: HospitalBody) => request<Schemas['CreatedResponse']>('POST', '/api/hospitals', { body }),
  updateHospital: (id: string, body: HospitalBody) => request<void>('PUT', `/api/hospitals/${id}`, { body }),
  setWorkingHours: (id: string, hours: WorkingHoursItem[]) => request<void>('PUT', `/api/hospitals/${id}/working-hours`, { body: { hours } }),
  deactivateHospital: (id: string) => request<void>('POST', `/api/hospitals/${id}/deactivate`),

  departments: (hospitalId: string, signal?: AbortSignal) => request<Department[]>('GET', `/api/hospitals/${hospitalId}/departments`, { signal }),
  createDepartment: (hospitalId: string, name: string) => request<Schemas['CreatedResponse']>('POST', `/api/hospitals/${hospitalId}/departments`, { body: { name } }),
  renameDepartment: (id: string, name: string) => request<void>('PUT', `/api/departments/${id}`, { body: { name } }),
  deactivateDepartment: (id: string) => request<void>('POST', `/api/departments/${id}/deactivate`),

  services: (hospitalId: string, signal?: AbortSignal) => request<AdminService[]>('GET', '/api/services', { query: { hospitalId }, signal }),
  createService: (body: CreateServiceBody) => request<Schemas['CreatedResponse']>('POST', '/api/services', { body }),
  updateService: (id: string, body: ServiceBody) => request<void>('PUT', `/api/services/${id}`, { body }),
  deactivateService: (id: string) => request<void>('POST', `/api/services/${id}/deactivate`),

  doctors: (query: { hospitalId?: string; departmentId?: string } & Page, signal?: AbortSignal) => request<AdminDoctor[]>('GET', '/api/doctors', { query, signal }),
  createDoctor: (body: CreateDoctorBody) => request<Schemas['CreatedResponse']>('POST', '/api/doctors', { body }),
  updateDoctor: (id: string, body: UpdateDoctorBody) => request<void>('PUT', `/api/doctors/${id}`, { body }),

  staff: (query: { hospitalId?: string } & Page, signal?: AbortSignal) => request<Staff[]>('GET', '/api/staff', { query, signal }),
  createStaff: (body: CreateStaffBody) => request<Schemas['CreatedResponse']>('POST', '/api/staff', { body }),

  activateUser: (id: string) => request<void>('POST', `/api/users/${id}/activate`),
  deactivateUser: (id: string) => request<void>('POST', `/api/users/${id}/deactivate`),

  serviceRequests: (query: { status?: ServiceRequestStatus } & Page, signal?: AbortSignal) =>
    request<ServiceRequest[]>('GET', '/api/doctor-service-requests', { query, signal }),
  approveServiceRequest: (doctorId: string, serviceId: string) => request<void>('POST', `/api/doctors/${doctorId}/services/${serviceId}/approve`),
  rejectServiceRequest: (doctorId: string, serviceId: string) => request<void>('POST', `/api/doctors/${doctorId}/services/${serviceId}/reject`),

  appointments: (query: { hospitalId?: string; status?: AppointmentStatus; from?: string; to?: string } & Page, signal?: AbortSignal): Promise<ApiResponse<Appointment[]>> =>
    request<Appointment[]>('GET', '/api/appointments', { query, signal }),
  cancelAppointment: (id: string, reason: string) => request<void>('POST', `/api/appointments/${id}/cancel`, { body: { reason } }),
}
