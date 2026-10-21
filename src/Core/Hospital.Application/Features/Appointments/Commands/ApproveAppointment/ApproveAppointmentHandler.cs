using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Commands.ApproveAppointment;

public sealed class ApproveAppointmentHandler(
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<ApproveAppointmentCommand, Result>
{
    public async ValueTask<Result> Handle(ApproveAppointmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await AppointmentAccess.LoadForDoctorAsync(command.AppointmentId, appointments, currentUser, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var appointment = loaded.Value;
        if (!appointment.Allows(AppointmentAction.Approve))
        {
            return AppointmentErrors.InvalidTransition(appointment.State.Label);
        }

        appointment.Approve(currentUser.UserId!.Value, clock.GetUtcNow());

        // Eşzamanlı çift karar xmin eşzamanlılık belirteciyle yakalanır (DataConflictException → 409).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
