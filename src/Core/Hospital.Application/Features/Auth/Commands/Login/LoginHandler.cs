using Hospital.Application.Wrappers;
using Hospital.Application.Interfaces;
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
    IUnitOfWork unitOfWork,
    AuthSettings settings,
    TimeProvider clock) : ICommandHandler<LoginCommand, Result<AuthenticatedUser>>
{
    public async ValueTask<Result<AuthenticatedUser>> Handle(LoginCommand command, CancellationToken cancellationToken)
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthenticatedUser(
            user.Id,
            user.Email,
            RoleNames.From(user.Role),
            user.SecurityStamp,
            user.ScopeClaims);
    }
}
