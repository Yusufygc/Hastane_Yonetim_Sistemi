using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.UpdateDoctor;

public sealed class UpdateDoctorHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateDoctorCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateDoctorCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadForAdminAsync(
            command.Id, DoctorInclude.None, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (doctor, hospital) = loaded.Value;

        var department = await departments.GetByIdAsync(command.DepartmentId, cancellationToken);
        if (department is not { IsActive: true } || department.HospitalId != hospital.Id)
        {
            return DoctorErrors.DepartmentInvalid;
        }

        if (await doctors.DiplomaExistsAsync(command.DiplomaNo.Trim(), doctor.Id, cancellationToken))
        {
            return DoctorErrors.DiplomaTaken;
        }

        doctor.UpdateByAdmin(
            command.FirstName, command.LastName, department.Id, command.Specialty, command.DiplomaNo, command.Phone);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
