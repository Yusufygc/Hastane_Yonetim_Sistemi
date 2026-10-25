using FluentValidation;
using Hospital.Application.Validation;

namespace Hospital.Application.Features.Doctors.Queries.ListDoctorServiceRequests;

public sealed class ListDoctorServiceRequestsValidator : AbstractValidator<ListDoctorServiceRequestsQuery>
{
    public ListDoctorServiceRequestsValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => DoctorStatusFilter.All.Contains(status!.Trim().ToUpperInvariant()))
            .WithMessage("Durum PENDING, APPROVED veya REJECTED olmalıdır.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Page).MustBeValidPage();
        RuleFor(x => x.PageSize).MustBeValidPageSize();
    }
}
