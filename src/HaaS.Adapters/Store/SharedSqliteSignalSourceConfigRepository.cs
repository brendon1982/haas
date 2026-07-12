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
}
