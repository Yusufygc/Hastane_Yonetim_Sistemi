using Hospital.Application.Common;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.RegisterPatient;

/// <summary>Herkese açık kayıt yalnızca hasta hesabı açar.</summary>
public sealed record RegisterPatientCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string IdentityNumber,
    DateOnly BirthDate,
    string? Phone) : ICommand<Result<Guid>>;
