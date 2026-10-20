using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Patients.Commands.UpdateMyPatientProfile;

public sealed class UpdateMyPatientProfileHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateMyPatientProfileCommand, Result<PatientProfileDto>>
{
    public async ValueTask<Result<PatientProfileDto>> Handle(UpdateMyPatientProfileCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        if (await users.GetByIdAsync(userId, cancellationToken) is not Patient patient)
        {
            return PatientErrors.NotFound;
        }

        patient.UpdateProfile(command.FirstName, command.LastName, command.Phone, command.BloodType);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return PatientProfileDto.From(patient);
    }
}
