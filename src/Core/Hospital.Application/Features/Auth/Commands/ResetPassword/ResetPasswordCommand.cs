using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// E-postadaki bağlantıdaki belirteçle yeni parola belirler. Başarıda güvenlik damgası döner (mevcut erişim belirteçleri düşer),
/// tüm yenileme belirteçleri iptal edilir, hesap kilidi kalkar ve belirteç bir daha kullanılamaz; yeni parolayla giriş gerekir.
/// </summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Result>;
