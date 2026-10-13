using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.DeactivateHospital;

/// <summary>Şubeyi pasifleştirir (silinmez). Pasif şube katalogda görünmez.</summary>
public sealed record DeactivateHospitalCommand(Guid Id) : ICommand<Result>;
