namespace Hospital.Application.Common;

/// <summary>
/// Veritabanı kısıtı (benzersizlik veya exclusion) eşzamanlı yarış nedeniyle ihlal edildi.
/// API katmanında 409 Conflict'e çevrilir.
/// </summary>
public sealed class DataConflictException(string constraintName)
    : Exception($"Veri çakışması: {constraintName}")
{
    public string ConstraintName { get; } = constraintName;
}
