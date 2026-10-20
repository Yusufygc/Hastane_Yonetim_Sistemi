using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Patients.Commands.UpdateMyPatientProfile;

/// <summary>Hasta kendi profilini günceller. T.C. kimlik no ve doğum tarihi değiştirilemez.</summary>
/// <param name="BloodType">A+, A-, B+, B-, AB+, AB-, 0+, 0- veya boş.</param>
public sealed record UpdateMyPatientProfileCommand(
    string FirstName,
    string LastName,
    string? Phone,
    string? BloodType) : ICommand<Result<PatientProfileDto>>;
