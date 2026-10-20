using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Commands.UpdateMyDoctorProfile;

/// <summary>Doktor kendi profilinde yalnızca uzmanlık ve telefonu değiştirebilir (ad, diploma, bölüm yöneticiye aittir).</summary>
public sealed record UpdateMyDoctorProfileCommand(string Specialty, string? Phone) : ICommand<Result>;
