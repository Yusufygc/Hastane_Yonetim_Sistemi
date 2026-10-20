using FluentValidation;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Doctors.Commands.AddMyTimeOff;

public sealed class AddMyTimeOffValidator : AbstractValidator<AddMyTimeOffCommand>
{
    public AddMyTimeOffValidator()
    {
        RuleFor(x => x.StartAt).Must(date => date != default).WithMessage("İzin başlangıcı zorunludur (ISO 8601).");
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt).WithMessage("İzin bitişi başlangıçtan sonra olmalıdır.");
        RuleFor(x => x.Reason)
            .MaximumLength(DoctorTimeOff.ReasonMaxLength).WithMessage($"İzin nedeni en fazla {DoctorTimeOff.ReasonMaxLength} karakter olabilir.");
    }
}
