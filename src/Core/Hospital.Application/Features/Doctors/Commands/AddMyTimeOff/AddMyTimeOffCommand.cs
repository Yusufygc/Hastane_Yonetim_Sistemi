using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.AddMyTimeOff;

/// <summary>İzin aralığı ekler (ISO 8601, zaman dilimli). Bu aralıkta slot üretilmez.</summary>
public sealed record AddMyTimeOffCommand(DateTimeOffset StartAt, DateTimeOffset EndAt, string? Reason) : ICommand<Result<Guid>>;
