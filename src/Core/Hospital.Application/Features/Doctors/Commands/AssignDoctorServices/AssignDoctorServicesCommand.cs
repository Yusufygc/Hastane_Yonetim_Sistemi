using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.AssignDoctorServices;

/// <summary>
/// Yönetici doktorun <b>onaylı</b> hizmet kümesini verilen listeyle eşitler: listedekiler onay beklemeden onaylanır,
/// listede olmayan onaylı hizmetler kaldırılır. Bekleyen/reddedilen talepler listede yoksa dokunulmaz.
/// </summary>
public sealed record AssignDoctorServicesCommand(Guid DoctorId, IReadOnlyList<Guid> ServiceIds) : ICommand<Result>;
