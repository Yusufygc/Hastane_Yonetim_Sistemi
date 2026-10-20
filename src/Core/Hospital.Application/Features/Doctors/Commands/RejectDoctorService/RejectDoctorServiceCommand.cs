using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RejectDoctorService;

public sealed record RejectDoctorServiceCommand(Guid DoctorId, Guid ServiceId) : ICommand<Result>;
