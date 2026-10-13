using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Commands.DeactivateService;

public sealed class DeactivateServiceHandler(
    IHospitalRepository hospitals,
    IServiceRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<DeactivateServiceCommand, Result>
{
    public async ValueTask<Result> Handle(DeactivateServiceCommand command, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        var service = await services.GetByIdAsync(command.Id, cancellationToken);
        if (service is null)
        {
            return scope is null ? HospitalErrors.Forbidden : ServiceErrors.NotFound;
        }

        var hospital = await hospitals.GetByIdAsync(service.HospitalId, includeWorkingHours: false, cancellationToken);
        if (HospitalErrors.CheckAccess(scope, hospital) is not null)
        {
            return scope is null ? HospitalErrors.Forbidden : ServiceErrors.NotFound;
        }

        service.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
