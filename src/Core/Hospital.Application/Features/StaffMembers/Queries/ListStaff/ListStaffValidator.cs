using FluentValidation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.StaffMembers.Queries.ListStaff;

public sealed class ListStaffValidator : AbstractValidator<ListStaffQuery>
{
    public ListStaffValidator()
    {
        RuleFor(x => x.Page).MustBeValidPage();
        RuleFor(x => x.PageSize).MustBeValidPageSize();
    }
}
