using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Commands.DeactivateService;

/// <summary>Hizmeti pasifleştirir (silinmez); geçmiş randevular snapshot ile korunur.</summary>
public sealed record DeactivateServiceCommand(Guid Id) : ICommand<Result>;
