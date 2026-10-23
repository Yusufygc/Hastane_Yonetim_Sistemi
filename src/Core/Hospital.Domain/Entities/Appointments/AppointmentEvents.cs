using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Appointments;

// Domain event'ler aggregate içinde üretilir ve SaveChanges sırasında (aynı transaction'da) dağıtılır; e-posta
// bildirimleri EMAIL_OUTBOX satırına çevrilir (Transactional Outbox). Olaylar yalnız kimlik ve alıcıya gösterilecek
// kişisel olmayan bilgileri taşır; kişi bilgileri (e-posta, ad) dağıtım sırasında okunur.

public sealed record AppointmentRequestedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid HospitalId,
    string ServiceName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt) : DomainEvent;

public sealed record AppointmentApprovedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid HospitalId,
    string ServiceName,
    DateTimeOffset StartAt) : DomainEvent;

public sealed record AppointmentRejectedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid HospitalId,
    string ServiceName,
    DateTimeOffset StartAt,
    string Reason) : DomainEvent;

public sealed record AppointmentCancelledEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid HospitalId,
    string ServiceName,
    DateTimeOffset StartAt,
    Guid CancelledBy,
    string Reason) : DomainEvent;
