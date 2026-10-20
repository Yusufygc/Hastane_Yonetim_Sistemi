using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Patients;

/// <param name="MaskedIdentityNumber">T.C. kimlik no maskeli (ilk 3 + son 2 hane görünür).</param>
public sealed record PatientProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string MaskedIdentityNumber,
    DateOnly BirthDate,
    string? BloodType,
    string? Phone)
{
    public static PatientProfileDto From(Patient patient) =>
        new(
            patient.Id,
            patient.Email,
            patient.FirstName,
            patient.LastName,
            TurkishIdentityNumber.Mask(patient.IdentityNumber),
            patient.BirthDate,
            patient.BloodType,
            patient.Phone);
}

public static class PatientErrors
{
    public static readonly Wrappers.Error NotFound =
        Wrappers.Error.NotFound("patient.not_found", "Hasta profili bulunamadı.");
}
