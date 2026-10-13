using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Commands.CreateService;

public sealed record CreateServiceCommand(
    Guid HospitalId,
    Guid? DepartmentId,
    string Name,
    int DurationMinutes,
    decimal Price,
    string Currency) : ICommand<Result<Guid>>;
