using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RemoveDoctorService;

/// <summary>Yönetici doktordan hizmeti (durumu ne olursa olsun) kaldırır. Gelecek randevu denetimi Faz 4'te eklenecek.</summary>
public sealed record RemoveDoctorServiceCommand(Guid DoctorId, Guid ServiceId) : ICommand<Result>;
