using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.ApproveDoctorService;

public sealed record ApproveDoctorServiceCommand(Guid DoctorId, Guid ServiceId) : ICommand<Result>;
