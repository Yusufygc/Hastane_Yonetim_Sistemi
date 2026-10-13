using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.Logout;

public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var token = await refreshTokens.GetByHashAsync(RefreshTokenSecret.Hash(command.RefreshToken), cancellationToken);

        // Başkasının belirtecini iptal etmek mümkün olmamalı; bulunamayan/yabancı belirteç sessizce yok sayılır (varlık sızmaz).
        if (token is not null && token.UserId == currentUser.UserId && !token.IsRevoked)
        {
            token.Revoke(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
