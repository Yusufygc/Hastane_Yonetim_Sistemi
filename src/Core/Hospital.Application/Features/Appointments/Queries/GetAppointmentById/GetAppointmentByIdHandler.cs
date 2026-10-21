using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetAppointmentById;

public sealed class GetAppointmentByIdHandler(
    IAppointmentReader reader,
    IHospitalRepository hospitals,
    ICurrentUser currentUser) : IQueryHandler<GetAppointmentByIdQuery, Result<AppointmentDto>>
{
    public async ValueTask<Result<AppointmentDto>> Handle(GetAppointmentByIdQuery query, CancellationToken cancellationToken)
    {
        var appointment = await reader.GetAsync(query.Id, cancellationToken);
        if (appointment is null)
        {
            return AppointmentErrors.NotFound;
        }

        var accessError = await AppointmentAccess.CheckViewAccessAsync(
            appointment.PatientId, appointment.DoctorId, appointment.HospitalId, hospitals, currentUser, cancellationToken);

        return accessError is null ? appointment : accessError;
    }
}
