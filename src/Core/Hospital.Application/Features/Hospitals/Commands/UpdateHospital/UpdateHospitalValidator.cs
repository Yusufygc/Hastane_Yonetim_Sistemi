using FluentValidation;
using Hospital.Application.Features.Hospitals.Validation;
using Hospital.Domain.Entities.Facility;

namespace Hospital.Application.Features.Hospitals.Commands.UpdateHospital;

public sealed class UpdateHospitalValidator : AbstractValidator<UpdateHospitalCommand>
{
    public UpdateHospitalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Şube adı zorunludur.").MaximumLength(HospitalBranch.NameMaxLength);
        RuleFor(x => x.Address).MaximumLength(HospitalBranch.AddressMaxLength);
        RuleFor(x => x.Phone).MaximumLength(HospitalBranch.PhoneMaxLength);
        RuleFor(x => x.TimeZoneId).MustBeValidTimeZone();
    }
}
