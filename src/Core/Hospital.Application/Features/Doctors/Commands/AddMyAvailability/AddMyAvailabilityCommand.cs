using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.AddMyAvailability;

/// <summary>Haftalık tekrar eden uygunluk penceresi ekler. Şubenin o günkü çalışma saatleri içinde olmalı ve diğer pencerelerle çakışmamalıdır.</summary>
/// <param name="ValidTo">Boşsa süresiz.</param>
public sealed record AddMyAvailabilityCommand(
    DayOfWeek Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly ValidFrom,
    DateOnly? ValidTo) : ICommand<Result<Guid>>;
