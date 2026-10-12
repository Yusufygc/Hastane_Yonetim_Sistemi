using FluentValidation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Auth.Commands.RegisterPatient;

public sealed class RegisterPatientValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .MaximumLength(254).WithMessage("E-posta en fazla 254 karakter olabilir.")
            .EmailAddress().WithMessage("E-posta biçimi geçersiz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parola zorunludur.")
            .MinimumLength(8).WithMessage("Parola en az 8 karakter olmalıdır.")
            .MaximumLength(128).WithMessage("Parola en fazla 128 karakter olabilir.")
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("Parola en az bir harf ve bir rakam içermelidir.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad zorunludur.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad zorunludur.")
            .MaximumLength(100);

        RuleFor(x => x.IdentityNumber)
            .Must(TurkishIdentityNumber.IsValid).WithMessage("T.C. kimlik numarası geçersiz.");

        RuleFor(x => x.BirthDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Doğum tarihi gelecekte olamaz.")
            .GreaterThan(new DateOnly(1900, 1, 1)).WithMessage("Doğum tarihi geçersiz.");

        RuleFor(x => x.Phone)
            .MaximumLength(32)
            .When(x => x.Phone is not null);
    }
}
