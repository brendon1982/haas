using System.Text.Json;
using Microsoft.Data.Sqlite;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class SharedSqliteSignalSourceConfigRepository : ISignalSourceConfigRepository
{
    private readonly string _connectionString;

    public SharedSqliteSignalSourceConfigRepository(string databasePath)
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
            @"CREATE TABLE IF NOT EXISTS signal_source_configs (
                SourceType TEXT PRIMARY KEY,
                Provider TEXT NOT NULL,
                ModelId TEXT NOT NULL,
                SystemPrompt TEXT NOT NULL,
                ToolBelt TEXT NOT NULL,
                ThinkingLevel TEXT NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    public async Task<SignalSourceConfig?> GetBySourceTypeAsync(string sourceType)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM signal_source_configs WHERE SourceType = $type";
        command.Parameters.AddWithValue("$type", sourceType);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SignalSourceConfig(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                JsonSerializer.Deserialize<ToolBelt>(reader.GetString(4)) ?? ToolBelt.Empty,
                reader.GetString(5)
            );
        }

        return null;
    }

    public async Task SaveAsync(SignalSourceConfig config)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO signal_source_configs (
                SourceType, Provider, ModelId, SystemPrompt, ToolBelt, ThinkingLevel
            ) VALUES (
                $type, $provider, $model, $prompt, $tools, $thinking
            ) ON CONFLICT(SourceType) DO UPDATE SET
                Provider = excluded.Provider,
                ModelId = excluded.ModelId,
                SystemPrompt = excluded.SystemPrompt,
                ToolBelt = excluded.ToolBelt,
                ThinkingLevel = excluded.ThinkingLevel;";

        command.Parameters.AddWithValue("$type", config.SourceType);
        command.Parameters.AddWithValue("$provider", config.Provider);
        command.Parameters.AddWithValue("$model", config.ModelId);
        command.Parameters.AddWithValue("$prompt", config.SystemPrompt);
        command.Parameters.AddWithValue("$tools", JsonSerializer.Serialize(config.ToolBelt));
        command.Parameters.AddWithValue("$thinking", config.ThinkingLevel);

        await command.ExecuteNonQueryAsync();
    }
}
