namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Yalnız test için sabit şifreleme anahtarı (Base64, tam 32 bayt).</summary>
public static class TestDataProtection
{
    public const string Key = "dGVzdC1vbmx5LWhvc3BpdGFsLWRhdGEta2V5LTAwMDI="; 

    /// <summary>Anahtar değişimi senaryoları için ikinci, farklı anahtar.</summary>
    public const string OtherKey = "b3RoZXItdGVzdC1vbmx5LWhvc3BpdGFsLWtleS0wMDM=";
}
