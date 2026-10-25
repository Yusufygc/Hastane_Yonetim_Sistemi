using Hospital.Application.Parameters;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Appointments.Queries.ListAppointments;

/// <summary>Yönetici kapsamındaki randevular; <c>HospitalId</c> verilirse yalnız o şube.</summary>
public sealed record ListAppointmentsQuery(
    Guid? HospitalId,
    string? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Page = null,
    int? PageSize = null) : IQuery<Result<PagedList<AppointmentDto>>>;
