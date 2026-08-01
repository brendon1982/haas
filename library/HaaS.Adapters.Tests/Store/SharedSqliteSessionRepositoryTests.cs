using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Adapters.Tests.Store;

[TestFixture]
public class SharedSqliteSessionRepositoryTests
{
    private string _dbPath = default!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Test]
    public async Task SaveAndLoad_ShouldPersistSessionRecord()
    {
        // Arrange
        var sut = new SharedSqliteSessionRepository(_dbPath);
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithSourceType("cli")
            .WithIdentityIssuer("issuer")
            .WithIdentitySubject("subject")
            .Build();

        // Act
        await sut.SaveAsync(record);
        var loaded = await sut.LoadAsync("sess-1");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.SessionId).To.Equal(record.SessionId);
        Expect(loaded.SourceType).To.Equal(record.SourceType);
        Expect(loaded.Status).To.Equal(record.Status);
        Expect(loaded.Provider).To.Equal(record.Provider);
        Expect(loaded.ModelId).To.Equal(record.ModelId);
        Expect(loaded.SystemPrompt).To.Equal(record.SystemPrompt);
        Expect(loaded.Tools).To.Equal(record.Tools);
        Expect(loaded.ThinkingLevel).To.Equal(record.ThinkingLevel);
        Expect(loaded.Output).To.Equal(record.Output);
        Expect(loaded.CreatedAt.ToUnixTimeSeconds()).To.Equal(record.CreatedAt.ToUnixTimeSeconds());
        Expect(loaded.UpdatedAt.ToUnixTimeSeconds()).To.Equal(record.UpdatedAt.ToUnixTimeSeconds());
        Expect(loaded.IdentityIssuer).To.Equal(record.IdentityIssuer);
        Expect(loaded.IdentitySubject).To.Equal(record.IdentitySubject);
    }

    [Test]
    public async Task Save_Twice_ShouldUpdateRecord()
    {
        // Arrange
        var sut = new SharedSqliteSessionRepository(_dbPath);
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithIdentityIssuer("issuer")
            .WithIdentitySubject("subject")
            .Build();
        await sut.SaveAsync(record);

        var updated = record with { Status = "completed", UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1) };

        // Act
        await sut.SaveAsync(updated);
        var loaded = await sut.LoadAsync("sess-1");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Status).To.Equal("completed");
        Expect(loaded.UpdatedAt.ToUnixTimeSeconds()).To.Equal(updated.UpdatedAt.ToUnixTimeSeconds());
    }

    [Test]
    public async Task Load_LegacyIdentityColumnsMissing_MapsToAnonymousIdentity()
    {
        // Arrange
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE sessions (
                    SessionId TEXT PRIMARY KEY, SourceType TEXT NOT NULL, Status TEXT NOT NULL,
                    Provider TEXT NOT NULL, ModelId TEXT NOT NULL, SystemPrompt TEXT NOT NULL,
                    Tools TEXT NOT NULL, ThinkingLevel TEXT NOT NULL, Output TEXT,
                    CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL
                );
                INSERT INTO sessions VALUES (
                    'sess-legacy', 'cli', 'completed', 'provider', 'model', 'prompt',
                    '[]', 'off', NULL, '2026-01-01T00:00:00.0000000+00:00',
                    '2026-01-01T00:00:00.0000000+00:00'
                );
                """;
            await command.ExecuteNonQueryAsync();
        }
        var sut = new SharedSqliteSessionRepository(_dbPath);

        // Act
        var loaded = await sut.LoadAsync("sess-legacy");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.IdentityIssuer).To.Equal(Identity.Anonymous.Issuer);
        Expect(loaded.IdentitySubject).To.Equal(Identity.Anonymous.Subject);
    }

    [Test]
    public async Task Load_LegacyNullIdentity_MapsToCanonicalAnonymousIdentity()
    {
        // Arrange
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE sessions (
                    SessionId TEXT PRIMARY KEY, SourceType TEXT NOT NULL, Status TEXT NOT NULL,
                    Provider TEXT NOT NULL, ModelId TEXT NOT NULL, SystemPrompt TEXT NOT NULL,
                    Tools TEXT NOT NULL, ThinkingLevel TEXT NOT NULL, Output TEXT,
                    CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
                    IdentityIssuer TEXT, IdentitySubject TEXT
                );
                INSERT INTO sessions VALUES (
                    'sess-legacy-null', 'cli', 'completed', 'provider', 'model', 'prompt',
                    '[]', 'off', NULL, '2026-01-01T00:00:00.0000000+00:00',
                    '2026-01-01T00:00:00.0000000+00:00', NULL, 'stale-subject'
                );
                """;
            await command.ExecuteNonQueryAsync();
        }
        var sut = new SharedSqliteSessionRepository(_dbPath);

        // Act
        var loaded = await sut.LoadAsync("sess-legacy-null");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.IdentityIssuer).To.Equal(Identity.Anonymous.Issuer);
        Expect(loaded.IdentitySubject).To.Equal(Identity.Anonymous.Subject);
    }
}
