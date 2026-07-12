using Microsoft.Data.Sqlite;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class SharedSqliteProviderConfigRepository : IProviderConfigRepository
{
    private readonly string _connectionString;

    public SharedSqliteProviderConfigRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"CREATE TABLE IF NOT EXISTS provider_configs (
                Provider TEXT PRIMARY KEY,
                Endpoint TEXT NOT NULL,
                ApiKey TEXT
            );";
        command.ExecuteNonQuery();
    }

    public async Task<IReadOnlyList<ProviderConfig>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM provider_configs";

        var configs = new List<ProviderConfig>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            configs.Add(new ProviderConfig(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)
            ));
        }

        return configs;
    }

    public async Task<ProviderConfig?> GetAsync(string provider)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM provider_configs WHERE Provider = $provider";
        command.Parameters.AddWithValue("$provider", provider);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProviderConfig(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)
            );
        }

        return null;
    }

    public async Task SaveAsync(ProviderConfig config)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO provider_configs (Provider, Endpoint, ApiKey)
              VALUES ($provider, $endpoint, $apiKey)
              ON CONFLICT(Provider) DO UPDATE SET
                Endpoint = excluded.Endpoint,
                ApiKey = excluded.ApiKey;";

        command.Parameters.AddWithValue("$provider", config.Provider);
        command.Parameters.AddWithValue("$endpoint", config.Endpoint);
        command.Parameters.AddWithValue("$apiKey", (object?)config.ApiKey ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }
}
