using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.RefreshToken;

/// <summary>Yenileme belirteciyle yeni belirteç çifti alır (rotation: eski yenileme belirteci iptal edilir).</summary>
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthTokens>>;
