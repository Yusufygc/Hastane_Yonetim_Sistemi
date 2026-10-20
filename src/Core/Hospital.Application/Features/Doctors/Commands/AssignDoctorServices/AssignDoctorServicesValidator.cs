using FluentValidation;

namespace Hospital.Application.Features.Doctors.Commands.AssignDoctorServices;

public sealed class AssignDoctorServicesValidator : AbstractValidator<AssignDoctorServicesCommand>
{
    private const int MaxServices = 100;

    public AssignDoctorServicesValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();

        RuleFor(x => x.ServiceIds)
            .NotNull().WithMessage("Hizmet listesi zorunludur (boş liste tüm onaylı hizmetleri kaldırır).")
            .Must(ids => ids is null || ids.Count <= MaxServices)
            .WithMessage($"En fazla {MaxServices} hizmet atanabilir.");

        RuleForEach(x => x.ServiceIds)
            .NotEmpty().WithMessage("Hizmet kimliği boş olamaz.");
    }
}
