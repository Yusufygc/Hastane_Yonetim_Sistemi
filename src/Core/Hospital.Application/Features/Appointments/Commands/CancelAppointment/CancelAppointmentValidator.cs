using FluentValidation;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Appointments.Commands.CancelAppointment;

public sealed class CancelAppointmentValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("İptal nedeni zorunludur.")
            .MaximumLength(Appointment.ReasonMaxLength).WithMessage($"İptal nedeni en fazla {Appointment.ReasonMaxLength} karakter olabilir.");
    }
}
