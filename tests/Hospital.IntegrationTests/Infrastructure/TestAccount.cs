namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Her test için benzersiz e-posta ve geçerli T.C. kimlik numarası üretir (yalnız test verisi).</summary>
public sealed record TestAccount(string Email, string Password, string IdentityNumber)
{
    public const string ValidPassword = "Test1234x";

    private static long _counter = 100_000_000;

    public static TestAccount New() => new(
        $"hasta.{Guid.NewGuid():N}@example.test",
        ValidPassword,
        NextIdentityNumber());

    public object RegistrationBody() => new
    {
        Email,
        Password,
        FirstName = "Ayşe",
        LastName = "Yılmaz",
        IdentityNumber,
        BirthDate = new DateOnly(1990, 5, 17),
        Phone = "05550000000",
    };

    public object LoginBody() => new { Email, Password };

    public object LoginBody(string password) => new { Email, Password = password };

    /// <summary>9 haneli sayaçtan sağlama basamaklarını hesaplayarak geçerli bir T.C. numarası üretir.</summary>
    private static string NextIdentityNumber()
    {
        var first9 = Interlocked.Increment(ref _counter).ToString();
        var d = first9.Select(c => c - '0').ToArray();

        var tenth = ((((d[0] + d[2] + d[4] + d[6] + d[8]) * 7) - (d[1] + d[3] + d[5] + d[7])) % 10 + 10) % 10;
        var eleventh = (d.Sum() + tenth) % 10;

        return first9 + tenth + eleventh;
    }
}
