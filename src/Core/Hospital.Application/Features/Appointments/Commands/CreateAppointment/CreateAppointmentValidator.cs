using FluentValidation;
using Hospital.Domain.Entities.Appointments;

namespace Hospital.Application.Features.Appointments.Commands.CreateAppointment;

public sealed class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty().WithMessage("Doktor zorunludur.");
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("Hizmet zorunludur.");
        RuleFor(x => x.StartAt).Must(start => start != default).WithMessage("Randevu başlangıcı zorunludur (ISO 8601).");
        RuleFor(x => x.PatientNote)
            .MaximumLength(Appointment.NoteMaxLength).WithMessage($"Not en fazla {Appointment.NoteMaxLength} karakter olabilir.");
    }
}
