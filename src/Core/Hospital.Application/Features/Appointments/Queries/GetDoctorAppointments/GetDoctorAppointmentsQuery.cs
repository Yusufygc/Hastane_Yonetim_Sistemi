using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetDoctorAppointments;

/// <param name="From">Başlangıç zamanı alt sınırı (dahil).</param>
/// <param name="To">Başlangıç zamanı üst sınırı (dahil).</param>
/// <param name="Status">PENDING, APPROVED, REJECTED, CANCELLED veya COMPLETED; boşsa tümü.</param>
public sealed record GetDoctorAppointmentsQuery(DateTimeOffset? From, DateTimeOffset? To, string? Status)
    : IQuery<Result<IReadOnlyList<AppointmentDto>>>;
