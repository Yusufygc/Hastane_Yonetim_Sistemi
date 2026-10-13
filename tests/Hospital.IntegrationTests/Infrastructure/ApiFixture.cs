namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Geçici veritabanı + o veritabanına bağlı uygulama (migration'lar başlangıçta uygulanır).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgresFixture _database = new();
    private readonly List<HospitalApiFactory> _extraFactories = [];

    public HospitalApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        Factory = new HospitalApiFactory(_database.ConnectionString, includeProbeEndpoints: false);
    }

    public ApiClient CreateClient() => new(Factory.CreateClient());

    /// <summary>Rol politikası uçlarını (yalnız test) içeren, ayrı yapılandırılabilir ikinci bir uygulama örneği.</summary>
    public HospitalApiFactory CreateFactory(
        IReadOnlyDictionary<string, string?>? settings = null,
        bool includeProbeEndpoints = false)
    {
        var factory = new HospitalApiFactory(_database.ConnectionString, settings, includeProbeEndpoints);
        _extraFactories.Add(factory);
        return factory;
    }

    public async Task DisposeAsync()
    {
        foreach (var factory in _extraFactories)
        {
            await factory.DisposeAsync();
        }

        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
