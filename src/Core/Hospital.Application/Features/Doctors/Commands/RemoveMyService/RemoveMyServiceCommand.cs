using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveMyService;

/// <summary>Doktor bekleyen veya reddedilmiş talebini geri çeker. Onaylı hizmeti yalnızca yönetici kaldırabilir.</summary>
public sealed record RemoveMyServiceCommand(Guid ServiceId) : ICommand<Result>;
