using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.ChangePassword;

public sealed class ChangePasswordHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<ChangePasswordCommand, Result>
{
    public async ValueTask<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId
            || await users.GetByIdAsync(userId, cancellationToken) is not { } user)
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.CurrentPassword))
        {
            return AuthErrors.CurrentPasswordInvalid;
        }

        if (command.CurrentPassword == command.NewPassword)
        {
            return AuthErrors.PasswordUnchanged;
        }

        var now = clock.GetUtcNow();
        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword));

        foreach (var token in await refreshTokens.GetActiveByUserAsync(user.Id, now, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
