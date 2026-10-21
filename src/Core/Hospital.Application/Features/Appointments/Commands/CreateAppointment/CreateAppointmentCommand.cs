using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.CreateAppointment;

/// <summary>Hasta randevu talep eder. <paramref name="StartAt"/> doktorun müsait slotlarından biri olmalıdır (UTC anı).</summary>
public sealed record CreateAppointmentCommand(
    Guid DoctorId,
    Guid ServiceId,
    DateTimeOffset StartAt,
    string? PatientNote) : ICommand<Result<Guid>>;
