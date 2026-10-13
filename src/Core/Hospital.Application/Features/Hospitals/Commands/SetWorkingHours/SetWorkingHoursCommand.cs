using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.SetWorkingHours;

public sealed record WorkingHoursItem(DayOfWeek Day, TimeOnly OpenTime, TimeOnly CloseTime);

/// <summary>Haftalık çalışma saatlerini verilen listeyle eşitler; listede olmayan günler kapalıdır.</summary>
public sealed record SetWorkingHoursCommand(Guid HospitalId, IReadOnlyList<WorkingHoursItem> Hours) : ICommand<Result>;
