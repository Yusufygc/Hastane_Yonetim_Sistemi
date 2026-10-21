using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.ApproveAppointment;

/// <summary>İlgili randevunun doktoru bekleyen randevuyu onaylar.</summary>
public sealed record ApproveAppointmentCommand(Guid AppointmentId) : ICommand<Result>;
