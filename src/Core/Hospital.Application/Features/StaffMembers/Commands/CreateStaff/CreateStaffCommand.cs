using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.StaffMembers.Commands.CreateStaff;

/// <summary>Yönetici personel hesabı oluşturur (başlangıç parolasını yönetici verir).</summary>
public sealed record CreateStaffCommand(
    Guid HospitalId,
    Guid? DepartmentId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Title) : ICommand<Result<Guid>>;
