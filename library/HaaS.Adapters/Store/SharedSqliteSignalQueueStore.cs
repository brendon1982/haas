using Microsoft.Data.Sqlite;
using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class SharedSqliteSignalQueueStore : ISignalQueue
{
    private readonly string _connectionString;
    private readonly TimeProvider _timeProvider;

    public SharedSqliteSignalQueueStore(string databasePath, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            @"CREATE TABLE IF NOT EXISTS signal_queue (
                id TEXT PRIMARY KEY,
                session_id TEXT,
                source_type TEXT NOT NULL,
                source_metadata_json TEXT,
                identity_json TEXT,
                payload_json TEXT,
                status TEXT NOT NULL DEFAULT 'pending',
                created_at TEXT NOT NULL,
                picked_at TEXT,
                completed_at TEXT,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3,
                visible_at TEXT,
                last_error TEXT
            );";
        command.ExecuteNonQuery();

        // Migration for existing tables
        command.CommandText = "ALTER TABLE signal_queue ADD COLUMN visible_at TEXT;";
        try { command.ExecuteNonQuery(); } catch { }
        command.CommandText = "ALTER TABLE signal_queue ADD COLUMN last_error TEXT;";
        try { command.ExecuteNonQuery(); } catch { }
    }

    public async Task EnqueueAsync(Signal signal, Identity identity)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO signal_queue (
                id, session_id, source_type, identity_json, payload_json, status, created_at
            ) VALUES (
                $id, $sessionId, $source, $identity, $payload, 'pending', $createdAt
            );";

        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$sessionId", (object?)signal.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", signal.Source);
        command.Parameters.AddWithValue("$identity", JsonSerializer.Serialize(identity));
        command.Parameters.AddWithValue("$payload", signal.Payload);
        command.Parameters.AddWithValue("$createdAt", _timeProvider.GetUtcNow().ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<QueuedSignal?> DequeueAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var now = _timeProvider.GetUtcNow();
        var nowStr = now.ToString("O");
        
        var command = connection.CreateCommand();
        command.CommandText = 
            @"UPDATE signal_queue 
              SET status = 'processing', picked_at = $now 
              WHERE id = (
                  SELECT id FROM signal_queue 
                  WHERE status = 'pending' AND (visible_at IS NULL OR visible_at <= $now) 
                  ORDER BY created_at ASC LIMIT 1
              )
              RETURNING id, session_id, source_type, identity_json, payload_json, status, created_at, picked_at, completed_at, retry_count, max_retries, visible_at, last_error;";
        
        command.Parameters.AddWithValue("$now", nowStr);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var sessionId = reader.IsDBNull(1) ? null : reader.GetString(1);
            var sourceType = reader.GetString(2);
            var identityJson = reader.GetString(3);
            var payload = reader.GetString(4);
            var statusStr = reader.GetString(5);
            var createdAtStr = reader.GetString(6);
            var pickedAtStr = reader.IsDBNull(7) ? null : reader.GetString(7);
            var completedAtStr = reader.IsDBNull(8) ? null : reader.GetString(8);
            var retryCount = reader.GetInt32(9);
            var maxRetries = reader.GetInt32(10);
            var visibleAtStr = reader.IsDBNull(11) ? null : reader.GetString(11);
            var lastError = reader.IsDBNull(12) ? null : reader.GetString(12);

            return new QueuedSignal(
                id,
                new Signal(payload, sourceType, sessionId),
                JsonSerializer.Deserialize<Identity>(identityJson) ?? Identity.Anonymous,
                Enum.Parse<SignalStatus>(statusStr, true),
                DateTimeOffset.Parse(createdAtStr),
                pickedAtStr != null ? DateTimeOffset.Parse(pickedAtStr) : null,
                completedAtStr != null ? DateTimeOffset.Parse(completedAtStr) : null,
                retryCount,
                maxRetries,
                visibleAtStr != null ? DateTimeOffset.Parse(visibleAtStr) : null,
                lastError
            );
        }

        return null;
    }

    public async Task AckAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"UPDATE signal_queue SET status = 'completed', completed_at = $completedAt WHERE id = $id;";
        command.Parameters.AddWithValue("$completedAt", _timeProvider.GetUtcNow().ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task NackAsync(string id, string? error = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Fetch current retry count and max retries
        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = "SELECT retry_count, max_retries FROM signal_queue WHERE id = $id;";
        selectCommand.Parameters.AddWithValue("$id", id);
        
        int retryCount = 0;
        int maxRetries = 3;
        using (var reader = await selectCommand.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                retryCount = reader.GetInt32(0) + 1;
                maxRetries = reader.GetInt32(1);
            }
        }

        var status = retryCount >= maxRetries ? SignalStatus.Failed : SignalStatus.Pending;
        DateTimeOffset? visibleAt = null;
        if (status == SignalStatus.Pending)
        {
            visibleAt = _timeProvider.GetUtcNow().AddSeconds(Math.Pow(2, retryCount));
        }

        var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = 
            @"UPDATE signal_queue 
              SET status = $status, 
                  retry_count = $retryCount, 
                  visible_at = $visibleAt, 
                  last_error = $error 
              WHERE id = $id;";
        
        updateCommand.Parameters.AddWithValue("$status", status.ToString().ToLower());
        updateCommand.Parameters.AddWithValue("$retryCount", retryCount);
        updateCommand.Parameters.AddWithValue("$visibleAt", (object?)visibleAt?.ToString("O") ?? DBNull.Value);
        updateCommand.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        updateCommand.Parameters.AddWithValue("$id", id);
        
        await updateCommand.ExecuteNonQueryAsync();
    }
}
