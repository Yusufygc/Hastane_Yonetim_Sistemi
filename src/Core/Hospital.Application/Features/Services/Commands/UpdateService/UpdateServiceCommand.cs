using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Commands.UpdateService;

public sealed record UpdateServiceCommand(
    Guid Id,
    Guid? DepartmentId,
    string Name,
    int DurationMinutes,
    decimal Price,
    string Currency) : ICommand<Result>;
