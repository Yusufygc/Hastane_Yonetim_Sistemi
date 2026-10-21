using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetDoctorSchedule;

/// <summary>Doktorun onaylı randevu takvimi. <c>From</c> boşsa şimdi, <c>To</c> boşsa <c>From</c> + 7 gün.</summary>
public sealed record GetDoctorScheduleQuery(DateTimeOffset? From, DateTimeOffset? To)
    : IQuery<Result<IReadOnlyList<AppointmentDto>>>;
