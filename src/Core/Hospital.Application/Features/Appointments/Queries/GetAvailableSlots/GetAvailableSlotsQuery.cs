using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetAvailableSlots;

/// <summary>Doktorun belirli bir hizmet için, şubenin yerel <paramref name="Date"/> gününde müsait slotları (anonim).</summary>
public sealed record GetAvailableSlotsQuery(Guid DoctorId, Guid ServiceId, DateOnly Date) : IQuery<Result<AvailableSlotsDto>>;

/// <param name="TimeZoneId">Şubenin IANA saat dilimi; <c>LocalStart</c> bu dilimdedir.</param>
public sealed record AvailableSlotsDto(
    Guid DoctorId,
    Guid ServiceId,
    DateOnly Date,
    string TimeZoneId,
    int DurationMinutes,
    IReadOnlyList<SlotDto> Slots);

public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt, TimeOnly LocalStart);
