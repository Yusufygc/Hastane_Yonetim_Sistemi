using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Users.Commands.DeactivateUser;

public sealed class DeactivateUserHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<DeactivateUserCommand, Result>
{
    public async ValueTask<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var loaded = await UserAccountAccess.LoadForAdminAsync(
            command.UserId, currentUser, users, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var user = loaded.Value;

        if (user.Id == currentUser.UserId)
        {
            return UserErrors.CannotModifySelf;
        }

        var now = clock.GetUtcNow();
        user.Deactivate();

        foreach (var token in await refreshTokens.GetActiveByUserAsync(user.Id, now, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
