namespace Hospital.Domain.Entities.Users;

/// <summary>T.C. kimlik numarası doğrulaması (11 hane + sağlama algoritması).</summary>
public static class TurkishIdentityNumber
{
    public const int Length = 11;

    /// <summary>Kişisel veriyi korumak için maskeler: ilk 3 ve son 2 hane görünür (ör. 100******46).</summary>
    public static string Mask(string identityNumber) =>
        identityNumber.Length == Length
            ? string.Concat(identityNumber.AsSpan(0, 3), "******", identityNumber.AsSpan(9, 2))
            : new string('*', identityNumber.Length);

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != Length || value[0] == '0' || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var d = value.Select(c => c - '0').ToArray();

        var oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        var evenSum = d[1] + d[3] + d[5] + d[7];

        var tenth = (((oddSum * 7) - evenSum) % 10 + 10) % 10;
        if (d[9] != tenth)
        {
            return false;
        }

        return d.Take(10).Sum() % 10 == d[10];
    }
}
