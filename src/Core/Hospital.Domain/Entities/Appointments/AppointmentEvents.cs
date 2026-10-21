using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Appointments;

// Domain event'ler aggregate içinde üretilir. Faz 5'te SaveChanges sırasında aynı transaction'da EMAIL_OUTBOX satırına
// çevrilir (Transactional Outbox); o zamana kadar yalnızca aggregate üzerinde birikir.

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
    DateTimeOffset StartAt) : DomainEvent;

public sealed record AppointmentRejectedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    string Reason) : DomainEvent;

public sealed record AppointmentCancelledEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid CancelledBy,
    string Reason) : DomainEvent;
