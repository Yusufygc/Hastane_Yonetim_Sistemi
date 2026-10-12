using Hospital.Application.Common;
using Hospital.Application.Common.Abstractions;
using Hospital.Domain.Users;
using Mediator;

namespace Hospital.Application.Features.Auth.Commands.RegisterPatient;

public sealed class RegisterPatientHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RegisterPatientCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(RegisterPatientCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return AuthErrors.EmailTaken;
        }

        if (await users.PatientIdentityNumberExistsAsync(command.IdentityNumber, cancellationToken))
        {
            return AuthErrors.IdentityNumberTaken;
        }

        var patient = Patient.Register(
            email,
            passwordHasher.Hash(command.Password),
            command.FirstName,
            command.LastName,
            command.IdentityNumber,
            command.BirthDate,
            command.Phone,
            clock.GetUtcNow());

        users.Add(patient);

        // Eşzamanlı aynı e-posta/TC kaydı veritabanı benzersizlik kısıtında yakalanır (DataConflictException → 409).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return patient.Id;
    }
}
