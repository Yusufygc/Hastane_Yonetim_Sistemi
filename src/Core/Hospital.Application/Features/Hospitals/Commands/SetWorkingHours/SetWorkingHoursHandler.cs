using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.SetWorkingHours;

public sealed class SetWorkingHoursHandler(
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<SetWorkingHoursCommand, Result>
{
    public async ValueTask<Result> Handle(SetWorkingHoursCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(command.HospitalId, includeWorkingHours: true, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
        {
            return error;
        }

        hospital!.SetWorkingHours(command.Hours.Select(h => new WorkingHoursInput(h.Day, h.OpenTime, h.CloseTime)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
