using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.RejectAppointment;

/// <summary>İlgili randevunun doktoru bekleyen randevuyu reddeder; neden zorunludur ve hastaya iletilir.</summary>
public sealed record RejectAppointmentCommand(Guid AppointmentId, string Reason) : ICommand<Result>;
