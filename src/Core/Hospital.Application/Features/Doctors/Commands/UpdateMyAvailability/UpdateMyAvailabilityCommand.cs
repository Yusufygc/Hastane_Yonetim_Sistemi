using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.UpdateMyAvailability;

public sealed record UpdateMyAvailabilityCommand(
    Guid Id,
    DayOfWeek Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly ValidFrom,
    DateOnly? ValidTo) : ICommand<Result>;
