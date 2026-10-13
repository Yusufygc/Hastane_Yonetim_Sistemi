using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Departments.Commands.DeactivateDepartment;

/// <summary>Bölümü pasifleştirir. Aktif hizmeti olan bölüm pasifleştirilemez.</summary>
public sealed record DeactivateDepartmentCommand(Guid Id) : ICommand<Result>;
