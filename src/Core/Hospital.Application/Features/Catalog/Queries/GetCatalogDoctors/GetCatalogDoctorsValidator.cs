using FluentValidation;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogDoctors;

public sealed class GetCatalogDoctorsValidator : AbstractValidator<GetCatalogDoctorsQuery>
{
    public GetCatalogDoctorsValidator()
    {
        RuleFor(x => x.HospitalId).NotEmpty().WithMessage("Şube (hospitalId) zorunludur.");
    }
}
