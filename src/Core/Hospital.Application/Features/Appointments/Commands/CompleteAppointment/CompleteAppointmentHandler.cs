using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.CompleteAppointment;

public sealed class CompleteAppointmentHandler(
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CompleteAppointmentCommand, Result>
{
    public async ValueTask<Result> Handle(CompleteAppointmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await AppointmentAccess.LoadForDoctorAsync(command.AppointmentId, appointments, currentUser, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var appointment = loaded.Value;
        var now = clock.GetUtcNow();

        if (!appointment.Allows(AppointmentAction.Complete))
        {
            return AppointmentErrors.InvalidTransition(appointment.State.Label);
        }

        if (!appointment.HasStarted(now))
        {
            return AppointmentErrors.NotStarted;
        }

        appointment.Complete(currentUser.UserId!.Value, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
