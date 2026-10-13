using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.Login;

/// <summary>
/// Hata mesajları kasıtlı olarak aynıdır: yanlış parola, bilinmeyen e-posta, pasif hesap ve kilitli hesap
/// aynı yanıtı verir (hesap varlığı sızdırılmaz).
/// </summary>
public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    AuthTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    AuthSettings settings,
    TimeProvider clock) : ICommandHandler<LoginCommand, Result<AuthTokens>>
{
    public async ValueTask<Result<AuthTokens>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var user = await users.GetByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        if (user is null)
        {
            // Zamanlama farkından hesap varlığı anlaşılmasın diye aynı maliyette bir işlem yapılır.
            passwordHasher.Hash(command.Password);
            return AuthErrors.InvalidCredentials;
        }

        if (!user.IsActive || user.IsLockedOut(now))
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            user.RecordFailedLogin(now, settings.MaxFailedLoginAttempts, settings.LockoutDuration);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidCredentials;
        }

        user.RecordSuccessfulLogin(now);
        var issued = tokenIssuer.Issue(user, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return issued.Tokens;
    }
}
