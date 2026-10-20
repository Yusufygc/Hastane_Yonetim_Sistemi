using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.ChangePassword;

/// <summary>
/// Kullanıcı kendi parolasını değiştirir. Başarıda güvenlik damgası döner (mevcut erişim belirteçleri düşer) ve
/// tüm yenileme belirteçleri iptal edilir; yeni parolayla yeniden giriş gerekir.
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Result>;
