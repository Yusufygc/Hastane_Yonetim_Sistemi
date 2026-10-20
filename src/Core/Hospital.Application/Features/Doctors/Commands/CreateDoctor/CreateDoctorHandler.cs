using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.CreateDoctor;

public sealed class CreateDoctorHandler(
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IDoctorRepository doctors,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CreateDoctorCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateDoctorCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var department = await departments.GetByIdAsync(command.DepartmentId, cancellationToken);
        var hospital = department is null
            ? null
            : await hospitals.GetByIdAsync(department.HospitalId, includeWorkingHours: false, cancellationToken);

        // Kapsam dışı veya olmayan bölüm aynı şekilde "bulunamadı" (varlık sızmaz).
        if (HospitalErrors.CheckAccess(scope, hospital) is { } accessError)
        {
            return accessError.Type == ErrorType.Forbidden ? accessError : DepartmentErrors.NotFound;
        }

        if (!department!.IsActive)
        {
            return DoctorErrors.DepartmentInvalid;
        }

        var email = User.NormalizeEmail(command.Email);

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return AuthErrors.EmailTaken;
        }

        if (await doctors.DiplomaExistsAsync(command.DiplomaNo.Trim(), null, cancellationToken))
        {
            return DoctorErrors.DiplomaTaken;
        }

        var doctor = Doctor.Create(
            email,
            passwordHasher.Hash(command.Password),
            command.FirstName,
            command.LastName,
            department.Id,
            command.Specialty,
            command.DiplomaNo,
            command.Phone,
            clock.GetUtcNow());

        doctors.Add(doctor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return doctor.Id;
    }
}
