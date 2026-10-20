using FluentValidation;
using Hospital.Application.Validation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Doctors.Commands.UpdateDoctor;

public sealed class UpdateDoctorValidator : AbstractValidator<UpdateDoctorCommand>
{
    public UpdateDoctorValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Bölüm zorunludur.");
        RuleFor(x => x.FirstName).MustBeValidPersonName("Ad", Doctor.NameMaxLength);
        RuleFor(x => x.LastName).MustBeValidPersonName("Soyad", Doctor.NameMaxLength);
        RuleFor(x => x.Specialty).MustBeValidPersonName("Uzmanlık", Doctor.SpecialtyMaxLength);
        RuleFor(x => x.DiplomaNo).MustBeValidPersonName("Diploma numarası", Doctor.DiplomaMaxLength);
        RuleFor(x => x.Phone).MustBeValidOptionalPhone();
    }
}
