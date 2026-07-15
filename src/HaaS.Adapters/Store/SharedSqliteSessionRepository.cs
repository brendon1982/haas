using Microsoft.Data.Sqlite;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class SharedSqliteSessionRepository : ISessionRepository
{
    private readonly string _connectionString;

    public SharedSqliteSessionRepository(string databasePath)
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
            @"CREATE TABLE IF NOT EXISTS sessions (
                SessionId TEXT PRIMARY KEY,
                SourceType TEXT NOT NULL,
                Status TEXT NOT NULL,
                Provider TEXT NOT NULL,
                ModelId TEXT NOT NULL,
                SystemPrompt TEXT NOT NULL,
                Tools TEXT NOT NULL,
                ThinkingLevel TEXT NOT NULL,
                Output TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    public async Task SaveAsync(SessionRecord record)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO sessions (
                SessionId, SourceType, Status, Provider, ModelId, 
                SystemPrompt, Tools, ThinkingLevel, Output, CreatedAt, UpdatedAt
            ) VALUES (
                $id, $source, $status, $provider, $model, 
                $prompt, $tools, $thinking, $output, $created, $updated
            ) ON CONFLICT(SessionId) DO UPDATE SET
                Status = excluded.Status,
                Provider = excluded.Provider,
                ModelId = excluded.ModelId,
                SystemPrompt = excluded.SystemPrompt,
                Tools = excluded.Tools,
                ThinkingLevel = excluded.ThinkingLevel,
                Output = excluded.Output,
                UpdatedAt = excluded.UpdatedAt;";

        command.Parameters.AddWithValue("$id", record.SessionId);
        command.Parameters.AddWithValue("$source", record.SourceType);
        command.Parameters.AddWithValue("$status", record.Status);
        command.Parameters.AddWithValue("$provider", record.Provider);
        command.Parameters.AddWithValue("$model", record.ModelId);
        command.Parameters.AddWithValue("$prompt", record.SystemPrompt);
        command.Parameters.AddWithValue("$tools", record.Tools);
        command.Parameters.AddWithValue("$thinking", record.ThinkingLevel);
        command.Parameters.AddWithValue("$output", (object?)record.Output ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", record.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", record.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<SessionRecord?> LoadAsync(string sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM sessions WHERE SessionId = $id";
        command.Parameters.AddWithValue("$id", sessionId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SessionRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                DateTimeOffset.Parse(reader.GetString(9)),
                DateTimeOffset.Parse(reader.GetString(10))
            );
        }

        return null;
    }
}
