using FluentValidation;
using Hospital.Application.Features.Appointments.Validation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Appointments.Queries.GetMyAppointments;

public sealed class GetMyAppointmentsValidator : AbstractValidator<GetMyAppointmentsQuery>
{
    public GetMyAppointmentsValidator()
    {
        RuleFor(x => x.Status).MustBeValidStatusFilter();
        RuleFor(x => x.Page).MustBeValidPage();
        RuleFor(x => x.PageSize).MustBeValidPageSize();
    }
}
