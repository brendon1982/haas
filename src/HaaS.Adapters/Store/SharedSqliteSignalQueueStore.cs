using Microsoft.Data.Sqlite;
using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class SharedSqliteSignalQueueStore : ISignalQueue
{
    private readonly string _connectionString;

    public SharedSqliteSignalQueueStore(string databasePath)
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
                max_retries INTEGER NOT NULL DEFAULT 3
            );";
        command.ExecuteNonQuery();
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
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<QueuedSignal?> DequeueAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Atomically pick a pending signal and mark it as processing
        using var transaction = connection.BeginTransaction();

        var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = 
            @"SELECT * FROM signal_queue 
              WHERE status = 'pending' AND retry_count < max_retries 
              ORDER BY created_at ASC LIMIT 1;";

        using var reader = await selectCommand.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var sessionId = reader.IsDBNull(1) ? null : reader.GetString(1);
            var sourceType = reader.GetString(2);
            var identityJson = reader.GetString(4);
            var payload = reader.GetString(5);
            var createdAtStr = reader.GetString(7);
            var retryCount = reader.GetInt32(10);
            var maxRetries = reader.GetInt32(11);

            reader.Close();

            var pickedAt = DateTimeOffset.UtcNow;
            var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = 
                @"UPDATE signal_queue SET status = 'processing', picked_at = $pickedAt WHERE id = $id;";
            updateCommand.Parameters.AddWithValue("$pickedAt", pickedAt.ToString("O"));
            updateCommand.Parameters.AddWithValue("$id", id);
            await updateCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            return new QueuedSignal(
                id,
                new Signal(payload, sourceType, sessionId),
                JsonSerializer.Deserialize<Identity>(identityJson) ?? Identity.Anonymous,
                SignalStatus.Processing,
                DateTimeOffset.Parse(createdAtStr),
                pickedAt,
                null,
                retryCount,
                maxRetries
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
        command.Parameters.AddWithValue("$completedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task NackAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"UPDATE signal_queue SET status = 'pending', retry_count = retry_count + 1 WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }
}
