using Npgsql;

namespace Hospital.IntegrationTests;

/// <summary>
/// Gerçek PostgreSQL üzerinde (docker compose) her test sınıfı için geçici bir veritabanı açar ve siler.
/// Yönetici bağlantısı TEST_POSTGRES_ADMIN ortam değişkeniyle değiştirilebilir (CI service container).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultAdminConnection =
        "Host=localhost;Port=5432;Database=postgres;Username=hospital;Password=hospital_dev";

    private readonly NpgsqlConnectionStringBuilder _admin = new(
        Environment.GetEnvironmentVariable("TEST_POSTGRES_ADMIN") ?? DefaultAdminConnection);

    private readonly string _databaseName = $"hospital_test_{Guid.NewGuid():N}";

    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_admin.ConnectionString) { Database = _databaseName }.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
