using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Commands.CreateDepartment;

public sealed record CreateDepartmentCommand(Guid HospitalId, string Name) : ICommand<Result<Guid>>;
