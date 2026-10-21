using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetMyAppointments;

/// <param name="Status">PENDING, APPROVED, REJECTED, CANCELLED veya COMPLETED; boşsa tümü.</param>
public sealed record GetMyAppointmentsQuery(string? Status) : IQuery<Result<IReadOnlyList<AppointmentDto>>>;
