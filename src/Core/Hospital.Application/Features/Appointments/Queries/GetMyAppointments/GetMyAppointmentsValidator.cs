using FluentValidation;
using Hospital.Application.Features.Appointments.Validation;

namespace Hospital.Application.Features.Appointments.Queries.GetMyAppointments;

public sealed class GetMyAppointmentsValidator : AbstractValidator<GetMyAppointmentsQuery>
{
    public GetMyAppointmentsValidator()
    {
        RuleFor(x => x.Status).MustBeValidStatusFilter();
    }
}
