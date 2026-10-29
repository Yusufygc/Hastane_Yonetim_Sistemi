using Hospital.Application.Features.Notifications;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetHandler(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    PasswordResetSettings settings,
    TimeProvider clock) : ICommandHandler<RequestPasswordResetCommand, Result>
{
    public async ValueTask<Result> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var user = await users.GetByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        // Bilinmeyen e-posta ve pasif hesap: sessizce başarılı (aynı yanıt; e-posta gitmez).
        if (user is null || !user.IsActive)
        {
            return Result.Success();
        }

        // E-posta bombardımanını önle: yakın zamanda zaten bağlantı gönderildiyse yenisini üretme.
        var usable = await resetTokens.GetUsableByUserAsync(user.Id, now, cancellationToken);
        if (usable.Any(t => now - t.CreatedAt < settings.ResendInterval))
        {
            return Result.Success();
        }

        // Yeni bağlantı istenince öncekiler geçersiz olur: aynı anda yalnız en yeni bağlantı çalışır.
        foreach (var previous in usable)
        {
            previous.MarkUsed(now);
        }

        var secret = RefreshTokenSecret.Generate();
        var token = PasswordResetToken.Issue(user.Id, RefreshTokenSecret.Hash(secret), now, settings.TokenLifetime);
        resetTokens.Add(token);

        var content = PasswordEmails.ResetRequested(user.FullName, settings.ResetLink(secret), settings.TokenLifetime);
        await outbox.EnqueueAsync(PasswordEmails.ToMessage(token.Id, "reset", user.Email, user.FullName, content, now), cancellationToken);

        // Belirteç ve e-posta satırı tek transaction'da: ya ikisi birden kaydolur ya hiçbiri.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
