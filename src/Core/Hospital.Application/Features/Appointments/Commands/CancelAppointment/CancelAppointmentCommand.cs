using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.CancelAppointment;

/// <summary>
/// Hasta (kendi), doktor (kendi) veya yönetici (kapsamındaki şube) randevuyu iptal eder; neden zorunludur.
/// Hasta, başlamasına <c>CancellationDeadlineHours</c> veya daha az kalmış onaylı randevuyu iptal edemez.
/// </summary>
public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason) : ICommand<Result>;
