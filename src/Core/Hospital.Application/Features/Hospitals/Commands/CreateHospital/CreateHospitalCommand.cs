using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.CreateHospital;

/// <summary>Yeni şube ekler; yalnızca süper yönetici (organizasyon kapsamı).</summary>
public sealed record CreateHospitalCommand(string Name, string? Address, string? Phone, string TimeZoneId)
    : ICommand<Result<Guid>>;
