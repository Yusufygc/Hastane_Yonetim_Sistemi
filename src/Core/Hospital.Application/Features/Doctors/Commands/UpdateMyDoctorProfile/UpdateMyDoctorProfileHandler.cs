using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.UpdateMyDoctorProfile;

public sealed class UpdateMyDoctorProfileHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateMyDoctorProfileCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateMyDoctorProfileCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.None, includeWorkingHours: false, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        loaded.Value.Doctor.UpdateOwnProfile(command.Specialty, command.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
