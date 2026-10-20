using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.StaffMembers.Commands.CreateStaff;

public sealed class CreateStaffHandler(
    IHospitalRepository hospitals,
    IDepartmentRepository departments,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CreateStaffCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateStaffCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(command.HospitalId, includeWorkingHours: false, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } accessError)
        {
            return accessError;
        }

        if (command.DepartmentId is { } departmentId)
        {
            var department = await departments.GetByIdAsync(departmentId, cancellationToken);
            if (department is not { IsActive: true } || department.HospitalId != hospital!.Id)
            {
                return StaffErrors.DepartmentInvalid;
            }
        }

        var email = User.NormalizeEmail(command.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return AuthErrors.EmailTaken;
        }

        var staff = Staff.Create(
            email,
            passwordHasher.Hash(command.Password),
            command.FirstName,
            command.LastName,
            command.Title,
            hospital!.Id,
            command.DepartmentId,
            clock.GetUtcNow());

        users.Add(staff);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return staff.Id;
    }
}
