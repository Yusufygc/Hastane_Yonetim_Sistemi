using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : ICommand<Result>;
