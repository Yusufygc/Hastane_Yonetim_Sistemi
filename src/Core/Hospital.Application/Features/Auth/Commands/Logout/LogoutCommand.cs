using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.Logout;

/// <summary>Oturumu kapatır: verilen yenileme belirtecini iptal eder. İdempotenttir.</summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand<Result>;
