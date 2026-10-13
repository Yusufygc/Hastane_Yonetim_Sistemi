namespace Hospital.Application.Features.Hospitals;

public sealed record HospitalDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Address,
    string? Phone,
    string TimeZoneId,
    bool IsActive);

public sealed record WorkingHoursDto(DayOfWeek Day, TimeOnly OpenTime, TimeOnly CloseTime);

public sealed record HospitalDetailDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Address,
    string? Phone,
    string TimeZoneId,
    bool IsActive,
    IReadOnlyList<WorkingHoursDto> WorkingHours);
