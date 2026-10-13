using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Organizations.Commands.UpdateOrganization;

public sealed record UpdateOrganizationCommand(string Name) : ICommand<Result>;
