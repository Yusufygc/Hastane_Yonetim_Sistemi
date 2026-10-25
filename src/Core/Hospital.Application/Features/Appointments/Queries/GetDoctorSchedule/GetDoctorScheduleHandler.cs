using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Parameters;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.GetDoctorSchedule;

public sealed class GetDoctorScheduleHandler(IAppointmentReader reader, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetDoctorScheduleQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);

    public async ValueTask<Result<IReadOnlyList<AppointmentDto>>> Handle(GetDoctorScheduleQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } doctorId)
        {
            return HospitalErrors.Forbidden;
        }

        var from = query.From ?? clock.GetUtcNow();
        var to = query.To ?? from + DefaultWindow;

        // Takvim penceresi sayfalanmaz (tek bakışta tüm pencere); en fazla PageRequest.MaxPageSize randevu.
        var page = await reader.ListForDoctorAsync(doctorId, from, to, AppointmentStatus.Approved, PageRequest.Largest, cancellationToken);
        return Result.Success(page.Items);
    }
}
