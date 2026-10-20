using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.CreateDoctor;

/// <summary>Yönetici doktor hesabı oluşturur (başlangıç parolasını yönetici verir; doktor sonra değiştirir).</summary>
public sealed record CreateDoctorCommand(
    Guid DepartmentId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Specialty,
    string DiplomaNo,
    string? Phone) : ICommand<Result<Guid>>;
