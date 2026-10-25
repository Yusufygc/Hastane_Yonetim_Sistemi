using FluentValidation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Doctors.Queries.ListDoctors;

public sealed class ListDoctorsValidator : AbstractValidator<ListDoctorsQuery>
{
    public ListDoctorsValidator()
    {
        RuleFor(x => x.Page).MustBeValidPage();
        RuleFor(x => x.PageSize).MustBeValidPageSize();
    }
}
