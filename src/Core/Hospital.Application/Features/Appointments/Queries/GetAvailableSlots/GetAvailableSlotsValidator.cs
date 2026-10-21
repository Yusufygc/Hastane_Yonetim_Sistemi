using FluentValidation;

namespace Hospital.Application.Features.Appointments.Queries.GetAvailableSlots;

public sealed class GetAvailableSlotsValidator : AbstractValidator<GetAvailableSlotsQuery>
{
    public GetAvailableSlotsValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty().WithMessage("Doktor zorunludur.");
        RuleFor(x => x.ServiceId).NotEmpty().WithMessage("Hizmet (serviceId) zorunludur.");
        RuleFor(x => x.Date).Must(date => date != default).WithMessage("Tarih (date) zorunludur (yyyy-MM-dd).");
    }
}
