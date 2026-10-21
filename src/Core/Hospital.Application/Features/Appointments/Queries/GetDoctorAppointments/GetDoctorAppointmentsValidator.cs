using FluentValidation;
using Hospital.Application.Features.Appointments.Validation;

namespace Hospital.Application.Features.Appointments.Queries.GetDoctorAppointments;

public sealed class GetDoctorAppointmentsValidator : AbstractValidator<GetDoctorAppointmentsQuery>
{
    public GetDoctorAppointmentsValidator()
    {
        RuleFor(x => x.Status).MustBeValidStatusFilter();
        RuleFor(x => x.To).MustNotPrecede(x => x.From);
    }
}
