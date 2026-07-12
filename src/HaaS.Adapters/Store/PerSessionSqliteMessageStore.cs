using Microsoft.Data.Sqlite;
using HaaS.Domain.Ports;

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
                Content TEXT NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<string>> GetMessagesAsync(string sessionId)
    {
        var connectionString = GetConnectionString(sessionId);
        await EnsureTableAsync(connectionString);

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Content FROM messages ORDER BY Id ASC";

        var messages = new List<string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(reader.GetString(0));
        }

        return messages;
    }

    public async Task AppendMessagesAsync(string sessionId, IEnumerable<string> messages)
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
            command.CommandText = "INSERT INTO messages (Content) VALUES ($content)";
            command.Parameters.AddWithValue("$content", message);
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
