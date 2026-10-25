using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Parameters;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetMyAppointments;

public sealed class GetMyAppointmentsHandler(IAppointmentReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetMyAppointmentsQuery, Result<PagedList<AppointmentDto>>>
{
    public async ValueTask<Result<PagedList<AppointmentDto>>> Handle(GetMyAppointmentsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } patientId)
        {
            return HospitalErrors.Forbidden;
        }

        AppointmentStatus? status = AppointmentStatusNames.TryParse(query.Status, out var parsed) ? parsed : null;
        return Result.Success(await reader.ListForPatientAsync(
            patientId, status, PageRequest.From(query.Page, query.PageSize), cancellationToken));
    }
}
