using FluentValidation;
using Hospital.Application.Validation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Patients.Commands.UpdateMyPatientProfile;

public sealed class UpdateMyPatientProfileValidator : AbstractValidator<UpdateMyPatientProfileCommand>
{
    public UpdateMyPatientProfileValidator()
    {
        RuleFor(x => x.FirstName).MustBeValidPersonName("Ad");
        RuleFor(x => x.LastName).MustBeValidPersonName("Soyad");
        RuleFor(x => x.Phone).MustBeValidOptionalPhone();

        RuleFor(x => x.BloodType)
            .Must(type => Patient.BloodTypes.Contains(type!.Trim().ToUpperInvariant()))
            .WithMessage("Kan grubu geçersiz (A+, A-, B+, B-, AB+, AB-, 0+, 0-).")
            .When(x => !string.IsNullOrWhiteSpace(x.BloodType));
    }
}
