using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Parameters;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.ListAppointments;

public sealed class ListAppointmentsHandler(
    IHospitalRepository hospitals,
    IAppointmentReader reader,
    ICurrentUser currentUser) : IQueryHandler<ListAppointmentsQuery, Result<PagedList<AppointmentDto>>>
{
    public async ValueTask<Result<PagedList<AppointmentDto>>> Handle(ListAppointmentsQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        if (query.HospitalId is { } hospitalId)
        {
            var hospital = await hospitals.GetByIdAsync(hospitalId, includeWorkingHours: false, cancellationToken);
            if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
            {
                return error;
            }
        }

        AppointmentStatus? status = AppointmentStatusNames.TryParse(query.Status, out var parsed) ? parsed : null;
        return Result.Success(
            await reader.ListForAdminAsync(
                scope, query.HospitalId, status, query.From, query.To, PageRequest.From(query.Page, query.PageSize), cancellationToken));
    }
}
