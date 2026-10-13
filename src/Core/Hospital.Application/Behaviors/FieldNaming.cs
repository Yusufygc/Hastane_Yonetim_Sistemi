namespace Hospital.Application.Behaviors;

/// <summary>Doğrulama hatası alan yollarını JSON adlandırmasına (camelCase) çevirir.</summary>
public static class FieldNaming
{
    /// <summary>"BirthDate" → "birthDate"; "Hours[0].CloseTime" → "hours[0].closeTime"; "" → "" (nesne düzeyi hata).</summary>
    public static string ToCamelCasePath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(ToCamelCase));

    private static string ToCamelCase(string segment) =>
        segment.Length == 0 || char.IsLower(segment[0])
            ? segment
            : char.ToLowerInvariant(segment[0]) + segment[1..];
}
