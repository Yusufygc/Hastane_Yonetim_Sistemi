using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Users.Commands.ActivateUser;

public sealed class ActivateUserHandler(
    IUserRepository users,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<ActivateUserCommand, Result>
{
    public async ValueTask<Result> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        var loaded = await UserAccountAccess.LoadForAdminAsync(
            command.UserId, currentUser, users, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        loaded.Value.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
