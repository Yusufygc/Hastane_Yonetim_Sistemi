using FluentValidation;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Appointments.Commands.RejectAppointment;

public sealed class RejectAppointmentValidator : AbstractValidator<RejectAppointmentCommand>
{
    public RejectAppointmentValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Red nedeni zorunludur.")
            .MaximumLength(Appointment.ReasonMaxLength).WithMessage($"Red nedeni en fazla {Appointment.ReasonMaxLength} karakter olabilir.");
    }
}
