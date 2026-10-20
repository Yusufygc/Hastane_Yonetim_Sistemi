using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Admins.Commands.CreateAdmin;

/// <summary>Süper yönetici yeni yönetici oluşturur. <c>BRANCH_MANAGER</c> için şube zorunlu, <c>SUPER_ADMIN</c> için verilmemelidir.</summary>
/// <param name="AccessLevel">SUPER_ADMIN veya BRANCH_MANAGER.</param>
public sealed record CreateAdminCommand(
    string AccessLevel,
    Guid? HospitalId,
    string Email,
    string Password,
    string FirstName,
    string LastName) : ICommand<Result<Guid>>;
