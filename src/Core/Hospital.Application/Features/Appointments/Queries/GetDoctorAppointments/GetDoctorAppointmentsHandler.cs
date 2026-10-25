using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Parameters;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetDoctorAppointments;

public sealed class GetDoctorAppointmentsHandler(IAppointmentReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetDoctorAppointmentsQuery, Result<PagedList<AppointmentDto>>>
{
    public async ValueTask<Result<PagedList<AppointmentDto>>> Handle(GetDoctorAppointmentsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } doctorId)
        {
            return HospitalErrors.Forbidden;
        }

        AppointmentStatus? status = AppointmentStatusNames.TryParse(query.Status, out var parsed) ? parsed : null;
        return Result.Success(await reader.ListForDoctorAsync(
            doctorId, query.From, query.To, status, PageRequest.From(query.Page, query.PageSize), cancellationToken));
    }
}
