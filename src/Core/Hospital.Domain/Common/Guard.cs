namespace Hospital.Domain.Common;

/// <summary>Domain invariant doğrulamaları; ihlalde <see cref="DomainException"/> fırlatır.</summary>
public static class Guard
{
    public static string NotBlank(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} boş olamaz.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} en fazla {maxLength} karakter olabilir.");
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} en fazla {maxLength} karakter olabilir.");
        }

        return trimmed;
    }

    public static Guid NotEmpty(Guid value, string fieldName) =>
        value == Guid.Empty ? throw new DomainException($"{fieldName} belirtilmelidir.") : value;
}
