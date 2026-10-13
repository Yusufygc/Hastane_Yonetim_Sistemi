using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.DeactivateHospital;

public sealed class DeactivateHospitalHandler(
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<DeactivateHospitalCommand, Result>
{
    public async ValueTask<Result> Handle(DeactivateHospitalCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(command.Id, includeWorkingHours: false, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
        {
            return error;
        }

        hospital!.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
