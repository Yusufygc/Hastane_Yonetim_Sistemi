using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Features.Services;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Users;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.RequestMyService;

public sealed class RequestMyServiceHandler(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IServiceRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<RequestMyServiceCommand, Result>
{
    public async ValueTask<Result> Handle(RequestMyServiceCommand command, CancellationToken cancellationToken)
    {
        var loaded = await DoctorAccess.LoadSelfAsync(
            DoctorInclude.Services, includeWorkingHours: false, currentUser, doctors, departments, hospitals, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (doctor, hospital) = loaded.Value;

        var service = await services.GetByIdAsync(command.ServiceId, cancellationToken);
        if (service is not { IsActive: true } || service.HospitalId != hospital.Id)
        {
            return DoctorErrors.ServiceInvalid;
        }

        switch (doctor.FindService(service.Id)?.Status)
        {
            case DoctorServiceStatus.Approved:
                return DoctorErrors.ServiceAlreadyApproved;
            case DoctorServiceStatus.Pending:
                return DoctorErrors.ServiceAlreadyRequested;
        }

        doctor.RequestService(service.Id, doctor.Id, clock.GetUtcNow());

        // Eşzamanlı çift talep (doctor_id, service_id) birincil anahtarında yakalanır (DataConflictException → 409).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
