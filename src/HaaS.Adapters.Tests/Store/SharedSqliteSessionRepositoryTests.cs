using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
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
        var record = new SessionRecord(
            "sess-1", "cli", "running", "openai", "gpt-4",
            "System prompt", "[]", "off",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

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
        Expect(loaded.CreatedAt.ToUnixTimeSeconds()).To.Equal(record.CreatedAt.ToUnixTimeSeconds());
        Expect(loaded.UpdatedAt.ToUnixTimeSeconds()).To.Equal(record.UpdatedAt.ToUnixTimeSeconds());
    }

    [Test]
    public async Task Save_Twice_ShouldUpdateRecord()
    {
        // Arrange
        var sut = new SharedSqliteSessionRepository(_dbPath);
        var record = new SessionRecord(
            "sess-1", "cli", "running", "openai", "gpt-4",
            "System prompt", "[]", "off",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
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
}
