using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RequestMyService;

/// <summary>Doktor kendi şubesindeki bir hizmeti sunmak için talep eder; yönetici onaylayana kadar randevu alınamaz.</summary>
public sealed record RequestMyServiceCommand(Guid ServiceId) : ICommand<Result>;
