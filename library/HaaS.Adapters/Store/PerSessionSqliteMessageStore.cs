using Microsoft.Data.Sqlite;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class PerSessionSqliteMessageStore : IMessageStore
{
    private readonly string _baseDirectory;

    public PerSessionSqliteMessageStore(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    private string GetConnectionString(string sessionId)
    {
        var dbPath = Path.Combine(_baseDirectory, $"{sessionId}.db");
        return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();
    }

    private async Task EnsureTableAsync(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = 
            @"CREATE TABLE IF NOT EXISTS messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Payload TEXT
            );";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<DomainMessage>> GetMessagesAsync(string sessionId)
    {
        var connectionString = GetConnectionString(sessionId);
        await EnsureTableAsync(connectionString);

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Role, Content, Timestamp, Payload FROM messages ORDER BY Id ASC";

        var messages = new List<DomainMessage>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new DomainMessage(
                reader.GetString(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return messages;
    }

    public async Task AppendMessagesAsync(string sessionId, IEnumerable<DomainMessage> messages)
    {
        var connectionString = GetConnectionString(sessionId);
        await EnsureTableAsync(connectionString);

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        foreach (var message in messages)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO messages (Role, Content, Timestamp, Payload) VALUES ($role, $content, $timestamp, $payload)";
            command.Parameters.AddWithValue("$role", message.Role);
            command.Parameters.AddWithValue("$content", message.Content);
            command.Parameters.AddWithValue("$timestamp", message.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$payload", (object?)message.Payload ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }
        transaction.Commit();
    }

    public async Task<int> GetMessageCountAsync(string sessionId)
    {
        var connectionString = GetConnectionString(sessionId);
        var dbPath = Path.Combine(_baseDirectory, $"{sessionId}.db");
        if (!File.Exists(dbPath)) return 0;

        await EnsureTableAsync(connectionString);

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM messages";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
