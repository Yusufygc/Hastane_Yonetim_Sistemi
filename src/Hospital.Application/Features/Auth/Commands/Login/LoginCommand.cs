using Hospital.Application.Common;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<AuthenticatedUser>>;
