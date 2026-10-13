using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Yenileme belirteci döndürme (rotation) ve yeniden kullanım tespiti:
/// <list type="bullet">
/// <item>Geçerli belirteç → iptal edilir, yerine yenisi (ve yeni erişim JWT'si) verilir.</item>
/// <item>Daha önce iptal edilmiş belirteç tekrar sunulursa çalınma şüphesi vardır → kullanıcının tüm etkin belirteçleri iptal edilir.</item>
/// <item>Bilinmeyen/süresi dolmuş belirteç veya pasif/kilitli hesap → genel "geçersiz oturum" hatası.</item>
/// </list>
/// </summary>
public sealed class RefreshTokenHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    AuthTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RefreshTokenCommand, Result<AuthTokens>>
{
    public async ValueTask<Result<AuthTokens>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var presented = await refreshTokens.GetByHashAsync(RefreshTokenSecret.Hash(command.RefreshToken), cancellationToken);

        if (presented is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (presented.IsRevoked)
        {
            // Rotation sonrası eski belirteç yeniden sunuldu: oturum ailesini kapat.
            foreach (var token in await refreshTokens.GetActiveByUserAsync(presented.UserId, now, cancellationToken))
            {
                token.Revoke(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        if (presented.IsExpired(now))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var user = await users.GetByIdAsync(presented.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.IsLockedOut(now))
        {
            presented.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var issued = tokenIssuer.Issue(user, now);
        presented.Revoke(now, issued.RefreshTokenRecord.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return issued.Tokens;
    }
}
