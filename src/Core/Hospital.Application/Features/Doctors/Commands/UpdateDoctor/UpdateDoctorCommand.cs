using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.UpdateDoctor;

/// <summary>Yönetici doktor bilgilerini günceller. Bölüm yalnızca doktorun şubesindeki başka bir bölüm olabilir.</summary>
public sealed record UpdateDoctorCommand(
    Guid Id,
    Guid DepartmentId,
    string FirstName,
    string LastName,
    string Specialty,
    string DiplomaNo,
    string? Phone) : ICommand<Result>;
