using FluentValidation;
using Hospital.Application.Features.Appointments.Validation;

namespace Hospital.Application.Features.Appointments.Queries.GetDoctorSchedule;

public sealed class GetDoctorScheduleValidator : AbstractValidator<GetDoctorScheduleQuery>
{
    public GetDoctorScheduleValidator()
    {
        RuleFor(x => x.To).MustNotPrecede(x => x.From);
    }
}
