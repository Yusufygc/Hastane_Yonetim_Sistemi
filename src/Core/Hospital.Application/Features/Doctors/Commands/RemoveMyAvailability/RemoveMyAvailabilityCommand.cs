using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveMyAvailability;

/// <summary>Uygunluk penceresini kaldırır. Mevcut randevuların etkilenmesi denetimi Faz 4'te eklenecek.</summary>
public sealed record RemoveMyAvailabilityCommand(Guid Id) : ICommand<Result>;
