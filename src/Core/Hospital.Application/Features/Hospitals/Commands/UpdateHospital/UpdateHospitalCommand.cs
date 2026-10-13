using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.UpdateHospital;

public sealed record UpdateHospitalCommand(Guid Id, string Name, string? Address, string? Phone, string TimeZoneId)
    : ICommand<Result>;
