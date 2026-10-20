using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Users.Commands.DeactivateUser;

/// <summary>Hesabı pasifleştirir: giriş engellenir, güvenlik damgası döner (erişim belirteçleri düşer) ve yenileme belirteçleri iptal edilir.</summary>
public sealed record DeactivateUserCommand(Guid UserId) : ICommand<Result>;
