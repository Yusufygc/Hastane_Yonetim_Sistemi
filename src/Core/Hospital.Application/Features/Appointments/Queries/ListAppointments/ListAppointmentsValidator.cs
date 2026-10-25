using FluentValidation;
using Hospital.Application.Features.Appointments.Validation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Appointments.Queries.ListAppointments;

public sealed class ListAppointmentsValidator : AbstractValidator<ListAppointmentsQuery>
{
    public ListAppointmentsValidator()
    {
        RuleFor(x => x.Status).MustBeValidStatusFilter();
        RuleFor(x => x.To).MustNotPrecede(x => x.From);
        RuleFor(x => x.Page).MustBeValidPage();
        RuleFor(x => x.PageSize).MustBeValidPageSize();
    }
}
