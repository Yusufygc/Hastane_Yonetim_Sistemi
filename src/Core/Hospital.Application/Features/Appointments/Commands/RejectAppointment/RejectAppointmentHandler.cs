using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.RejectAppointment;

public sealed class RejectAppointmentHandler(
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<RejectAppointmentCommand, Result>
{
    public async ValueTask<Result> Handle(RejectAppointmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await AppointmentAccess.LoadForDoctorAsync(command.AppointmentId, appointments, currentUser, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var appointment = loaded.Value;
        if (!appointment.Allows(AppointmentAction.Reject))
        {
            return AppointmentErrors.InvalidTransition(appointment.State.Label);
        }

        appointment.Reject(currentUser.UserId!.Value, command.Reason, clock.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
