using Hospital.Application.Common.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Hospital.Infrastructure.Features.Auth;

/// <summary>ASP.NET Core Identity'nin PBKDF2 tabanlı parola özeti algoritmasını kullanır.</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
