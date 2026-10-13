using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.UpdateHospital;

public sealed class UpdateHospitalHandler(
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateHospitalCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateHospitalCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var hospital = await hospitals.GetByIdAsync(command.Id, includeWorkingHours: false, cancellationToken);

        if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
        {
            return error;
        }

        if (await hospitals.NameExistsAsync(hospital!.OrganizationId, command.Name.Trim(), hospital.Id, cancellationToken))
        {
            return HospitalErrors.NameTaken;
        }

        hospital.Update(command.Name, command.Address, command.Phone, command.TimeZoneId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
