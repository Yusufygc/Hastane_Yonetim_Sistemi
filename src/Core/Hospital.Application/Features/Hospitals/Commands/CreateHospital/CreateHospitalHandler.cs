using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;
using Mediator;

namespace Hospital.Application.Features.Hospitals.Commands.CreateHospital;

public sealed class CreateHospitalHandler(
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<CreateHospitalCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateHospitalCommand command, CancellationToken cancellationToken)
    {
        // Yeni şube eklemek organizasyon kapsamı ister: yalnızca süper yönetici.
        var scope = AdminScope.From(currentUser);
        if (scope is not { IsSuperAdmin: true })
        {
            return HospitalErrors.Forbidden;
        }

        if (await hospitals.NameExistsAsync(scope.OrganizationId, command.Name.Trim(), null, cancellationToken))
        {
            return HospitalErrors.NameTaken;
        }

        var hospital = HospitalBranch.Create(scope.OrganizationId, command.Name, command.Address, command.Phone, command.TimeZoneId);
        hospitals.Add(hospital);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return hospital.Id;
    }
}
