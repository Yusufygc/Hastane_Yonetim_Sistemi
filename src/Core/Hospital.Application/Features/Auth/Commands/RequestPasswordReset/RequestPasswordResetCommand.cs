using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.RequestPasswordReset;

/// <summary>
/// "Şifremi unuttum": e-postaya tek kullanımlık sıfırlama bağlantısı gönderir. Yanıt, hesap var olsun olmasın <b>aynıdır</b>
/// (hesap varlığı sızdırılmaz); pasif hesaba ve kısa sürede tekrarlanan isteğe e-posta gitmez.
/// </summary>
public sealed record RequestPasswordResetCommand(string Email) : ICommand<Result>;
