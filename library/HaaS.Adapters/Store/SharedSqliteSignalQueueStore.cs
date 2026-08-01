using Microsoft.Data.Sqlite;
using System.Collections.Immutable;
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
                payload_json TEXT,
                context_json TEXT,
                arrived_at TEXT,
                message_id TEXT,
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

        AddColumnIfMissing(connection, "context_json", "TEXT");
        AddColumnIfMissing(connection, "arrived_at", "TEXT");
        AddColumnIfMissing(connection, "message_id", "TEXT");
        AddColumnIfMissing(connection, "visible_at", "TEXT");
        AddColumnIfMissing(connection, "last_error", "TEXT");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string columnName, string columnType)
    {
        var columnExistsCommand = connection.CreateCommand();
        columnExistsCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('signal_queue') WHERE name = $columnName;";
        columnExistsCommand.Parameters.AddWithValue("$columnName", columnName);
        if (Convert.ToInt64(columnExistsCommand.ExecuteScalar()) != 0)
        {
            return;
        }

        var addColumnCommand = connection.CreateCommand();
        addColumnCommand.CommandText = $"ALTER TABLE signal_queue ADD COLUMN {columnName} {columnType};";
        addColumnCommand.ExecuteNonQuery();
    }

    public async Task EnqueueAsync(SignalEnvelope envelope)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"INSERT INTO signal_queue (
                id, session_id, source_type, payload_json, context_json, arrived_at, message_id, status, created_at
            ) VALUES (
                $id, $sessionId, $source, $payload, $context, $arrivedAt, $messageId, 'pending', $createdAt
            );";

        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$sessionId", (object?)envelope.Signal.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", envelope.Signal.Source);
        command.Parameters.AddWithValue("$payload", envelope.Signal.Payload);
        command.Parameters.AddWithValue("$context", SerializeContext(envelope.Context));
        command.Parameters.AddWithValue("$arrivedAt", (object?)envelope.Signal.ArrivedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$messageId", (object?)envelope.Signal.MessageId ?? DBNull.Value);
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
              RETURNING id, session_id, source_type, payload_json, context_json, arrived_at, message_id, status, created_at, picked_at, completed_at, retry_count, max_retries, visible_at, last_error;";
        
        command.Parameters.AddWithValue("$now", nowStr);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var sessionId = reader.IsDBNull(1) ? null : reader.GetString(1);
            var sourceType = reader.GetString(2);
            var payload = reader.GetString(3);
            var context = reader.IsDBNull(4)
                ? SignalContext.Anonymous
                : DeserializeContext(reader.GetString(4));
            var arrivedAtStr = reader.IsDBNull(5) ? null : reader.GetString(5);
            var messageId = reader.IsDBNull(6) ? null : reader.GetString(6);
            var statusStr = reader.GetString(7);
            var createdAtStr = reader.GetString(8);
            var pickedAtStr = reader.IsDBNull(9) ? null : reader.GetString(9);
            var completedAtStr = reader.IsDBNull(10) ? null : reader.GetString(10);
            var retryCount = reader.GetInt32(11);
            var maxRetries = reader.GetInt32(12);
            var visibleAtStr = reader.IsDBNull(13) ? null : reader.GetString(13);
            var lastError = reader.IsDBNull(14) ? null : reader.GetString(14);

            return new QueuedSignal(
                id,
                new SignalEnvelope(
                    new Signal(
                        payload,
                        sourceType,
                        sessionId,
                        arrivedAtStr is null ? null : DateTimeOffset.Parse(arrivedAtStr),
                        messageId),
                    context),
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

    private static SignalContext DeserializeContext(string contextJson)
    {
        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedSignalContext>(contextJson)
                ?? throw new JsonException("Signal context JSON must not deserialize to null.");
            var authentication = persisted.Authentication
                ?? throw new JsonException("Signal context authentication is required.");
            var identity = authentication.Identity
                ?? throw new JsonException("Signal context identity is required.");
            var claims = identity.Claims
                ?? throw new JsonException("Signal context identity claims are required.");
            var attributes = persisted.Attributes
                ?? throw new JsonException("Signal context attributes are required.");
            var credentialReferences = authentication.CredentialReferences
                ?? throw new JsonException("Signal context credential references are required.");

            var domainIdentity = new Identity(
                identity.Issuer,
                identity.Subject,
                claims.ToImmutableDictionary(
                    claim => claim.Key,
                    claim => (claim.Value ?? throw new JsonException("Signal context claim values are required."))
                        .ToImmutableHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal));
            var domainCredentialReferences = credentialReferences.Values.Select(reference =>
            {
                if (reference is null)
                {
                    throw new JsonException("Signal context credential reference is required.");
                }

                return new CredentialReference(reference.Name, reference.Provider, reference.Reference);
            });
            return new SignalContext(
                new AuthenticationContext(domainIdentity, authentication.AuthenticationMethod, domainCredentialReferences),
                attributes);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException("Queued signal contains invalid context_json.", exception);
        }
    }

    private static string SerializeContext(SignalContext context)
    {
        var authentication = context.Authentication;
        var identity = authentication.Identity;
        return JsonSerializer.Serialize(new PersistedSignalContext(
            new PersistedAuthenticationContext(
                new PersistedIdentity(
                    identity.Issuer,
                    identity.Subject,
                    identity.Claims.ToDictionary(
                        claim => claim.Key,
                        claim => claim.Value.ToArray(),
                        StringComparer.Ordinal)),
                authentication.AuthenticationMethod,
                authentication.CredentialReferences.ToDictionary(
                    reference => reference.Key,
                    reference => new PersistedCredentialReference(
                        reference.Value.Name,
                        reference.Value.Provider,
                        reference.Value.Reference),
                    StringComparer.Ordinal)),
            context.Attributes.ToDictionary(
                attribute => attribute.Key,
                attribute => attribute.Value,
                StringComparer.Ordinal)));
    }

    private sealed record PersistedSignalContext(
        PersistedAuthenticationContext Authentication,
        Dictionary<string, string> Attributes);

    private sealed record PersistedAuthenticationContext(
        PersistedIdentity Identity,
        string AuthenticationMethod,
        Dictionary<string, PersistedCredentialReference> CredentialReferences);

    private sealed record PersistedIdentity(
        string Issuer,
        string Subject,
        Dictionary<string, string[]> Claims);

    private sealed record PersistedCredentialReference(
        string Name,
        string Provider,
        string Reference);

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
