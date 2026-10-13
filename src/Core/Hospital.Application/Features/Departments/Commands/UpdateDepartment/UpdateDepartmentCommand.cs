using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Commands.UpdateDepartment;

public sealed record UpdateDepartmentCommand(Guid Id, string Name) : ICommand<Result>;
