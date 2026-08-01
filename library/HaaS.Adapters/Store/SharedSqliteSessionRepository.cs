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
                UpdatedAt TEXT NOT NULL,
                IdentityIssuer TEXT,
                IdentitySubject TEXT
            );";
        command.ExecuteNonQuery();

        EnsureColumn(connection, "IdentityIssuer");
        EnsureColumn(connection, "IdentitySubject");
    }

    private static void EnsureColumn(SqliteConnection connection, string columnName)
    {
        using var columns = connection.CreateCommand();
        columns.CommandText = "PRAGMA table_info(sessions);";
        using var reader = columns.ExecuteReader();
        while (reader.Read())
        {
            if (StringComparer.Ordinal.Equals(reader.GetString(1), columnName))
            {
                return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE sessions ADD COLUMN {columnName} TEXT;";
        alter.ExecuteNonQuery();
    }

    public async Task SaveAsync(SessionRecord record)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO sessions (
                SessionId, SourceType, Status, Provider, ModelId, 
                SystemPrompt, Tools, ThinkingLevel, Output, CreatedAt, UpdatedAt,
                IdentityIssuer, IdentitySubject
            ) VALUES (
                $id, $source, $status, $provider, $model, 
                $prompt, $tools, $thinking, $output, $created, $updated,
                $issuer, $subject
            ) ON CONFLICT(SessionId) DO UPDATE SET
                SourceType = excluded.SourceType,
                Status = excluded.Status,
                Provider = excluded.Provider,
                ModelId = excluded.ModelId,
                SystemPrompt = excluded.SystemPrompt,
                Tools = excluded.Tools,
                ThinkingLevel = excluded.ThinkingLevel,
                Output = excluded.Output,
                UpdatedAt = excluded.UpdatedAt,
                IdentityIssuer = excluded.IdentityIssuer,
                IdentitySubject = excluded.IdentitySubject;";

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
        command.Parameters.AddWithValue("$issuer", record.IdentityIssuer);
        command.Parameters.AddWithValue("$subject", record.IdentitySubject);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<SessionRecord?> LoadAsync(string sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT SessionId, SourceType, Status, Provider, ModelId, SystemPrompt,
                   Tools, ThinkingLevel, Output, CreatedAt, UpdatedAt,
                   IdentityIssuer, IdentitySubject
            FROM sessions
            WHERE SessionId = $id
            """;
        command.Parameters.AddWithValue("$id", sessionId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var identity = reader.IsDBNull(11) || reader.IsDBNull(12)
                ? Identity.Anonymous
                : new Identity(reader.GetString(11), reader.GetString(12));

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
                DateTimeOffset.Parse(reader.GetString(10)),
                identity.Issuer,
                identity.Subject
            );
        }

        return null;
    }
}
