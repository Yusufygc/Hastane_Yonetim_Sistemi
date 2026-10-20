using FluentValidation;
using Hospital.Application.Validation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Doctors.Commands.UpdateMyDoctorProfile;

public sealed class UpdateMyDoctorProfileValidator : AbstractValidator<UpdateMyDoctorProfileCommand>
{
    public UpdateMyDoctorProfileValidator()
    {
        RuleFor(x => x.Specialty).MustBeValidPersonName("Uzmanlık", Doctor.SpecialtyMaxLength);
        RuleFor(x => x.Phone).MustBeValidOptionalPhone();
    }
}
