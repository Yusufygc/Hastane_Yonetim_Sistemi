using Hospital.Application.Features.Notifications;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.ResetPassword;

public sealed class ResetPasswordHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ResetPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // Bilinmeyen, süresi dolmuş, kullanılmış veya pasif hesaba ait belirteç: aynı hata (ayrım yapılmaz).
        var token = await resetTokens.GetByHashAsync(RefreshTokenSecret.Hash(command.Token.Trim()), cancellationToken);
        if (token is null || !token.IsUsable(now))
        {
            return AuthErrors.InvalidResetToken;
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return AuthErrors.InvalidResetToken;
        }

        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword)); // güvenlik damgası döner: eski erişim belirteçleri düşer
        user.ClearLockout();

        token.MarkUsed(now);
        foreach (var other in await resetTokens.GetUsableByUserAsync(user.Id, now, cancellationToken))
        {
            other.MarkUsed(now); // başka bekleyen bağlantılar da geçersiz
        }

        foreach (var refresh in await refreshTokens.GetActiveByUserAsync(user.Id, now, cancellationToken))
        {
            refresh.Revoke(now); // açık tüm oturumlar kapanır
        }

        // Hesap sahibine "parolanız değişti" bildirimi: izinsiz sıfırlama fark edilsin.
        var content = PasswordEmails.PasswordChanged(user.FullName);
        await outbox.EnqueueAsync(PasswordEmails.ToMessage(token.Id, "changed", user.Email, user.FullName, content, now), cancellationToken);

        // xmin eşzamanlılık belirteci: aynı bağlantıyla iki eşzamanlı sıfırlamadan biri 409 alır (tek kullanım).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
