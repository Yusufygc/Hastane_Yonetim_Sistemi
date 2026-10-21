using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetAvailableSlots;

public sealed class GetAvailableSlotsHandler(SlotAvailabilityService slots)
    : IQueryHandler<GetAvailableSlotsQuery, Result<AvailableSlotsDto>>
{
    public async ValueTask<Result<AvailableSlotsDto>> Handle(GetAvailableSlotsQuery query, CancellationToken cancellationToken)
    {
        var loaded = await slots.LoadAsync(query.DoctorId, query.ServiceId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var context = loaded.Value;
        var available = await slots.ComputeSlotsAsync(context, query.Date, cancellationToken);

        var items = available
            .Select(s => new SlotDto(
                s.Start,
                s.End,
                TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.Start, context.TimeZone).DateTime)))
            .ToList();

        return new AvailableSlotsDto(
            context.Doctor.Id,
            context.Service.Id,
            query.Date,
            context.Hospital.TimeZoneId,
            context.Service.DurationMinutes,
            items);
    }
}
