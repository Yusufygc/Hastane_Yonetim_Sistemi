using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveMyTimeOff;

public sealed record RemoveMyTimeOffCommand(Guid Id) : ICommand<Result>;
