using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.CompleteAppointment;

/// <summary>İlgili randevunun doktoru, başlamış onaylı randevuyu tamamlar.</summary>
public sealed record CompleteAppointmentCommand(Guid AppointmentId) : ICommand<Result>;
