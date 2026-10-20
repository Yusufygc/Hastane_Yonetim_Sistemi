using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Admins.Commands.CreateAdmin;

public sealed class CreateAdminHandler(
    IHospitalRepository hospitals,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CreateAdminCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateAdminCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        if (!scope.IsSuperAdmin)
        {
            return AdminErrors.SuperAdminRequired;
        }

        var email = User.NormalizeEmail(command.Email);
        var hashed = passwordHasher.Hash(command.Password);
        var now = clock.GetUtcNow();

        Admin admin;

        if (AdminAccessLevelNames.Parse(command.AccessLevel) == AdminAccessLevel.SuperAdmin)
        {
            admin = Admin.CreateSuperAdmin(email, hashed, command.FirstName, command.LastName, scope.OrganizationId, now);
        }
        else
        {
            var hospital = await hospitals.GetByIdAsync(command.HospitalId!.Value, includeWorkingHours: false, cancellationToken);
            if (HospitalErrors.CheckAccess(scope, hospital) is { } accessError)
            {
                return accessError;
            }

            admin = Admin.CreateBranchManager(email, hashed, command.FirstName, command.LastName, scope.OrganizationId, hospital!.Id, now);
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return AuthErrors.EmailTaken;
        }

        users.Add(admin);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return admin.Id;
    }
}
