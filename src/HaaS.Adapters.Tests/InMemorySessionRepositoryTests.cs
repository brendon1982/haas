using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Store;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class InMemorySessionRepositoryTests
{
    [Test]
    public async Task SaveAndLoad_RoundTrip_ReturnsSameRecord()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();
        var now = DateTime.UtcNow;
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithSourceType("cli")
            .WithStatus(SessionRecord.Statuses.Running)
            .WithCreatedAt(now)
            .WithUpdatedAt(now)
            .Build();

        // Act
        await sut.SaveAsync(record);
        var loaded = await sut.LoadAsync("sess-1");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.SessionId).To.Equal(record.SessionId);
        Expect(loaded.SourceType).To.Equal(record.SourceType);
        Expect(loaded.Status).To.Equal(record.Status);
        Expect(loaded.CreatedAt).To.Equal(now);
        Expect(loaded.UpdatedAt).To.Equal(now);
    }

    [Test]
    public async Task Load_MissingSession_ReturnsNull()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();

        // Act
        var loaded = await sut.LoadAsync("nonexistent");

        // Assert
        Expect(loaded).To.Be.Null();
    }

    [Test]
    public async Task Save_OverwriteExisting_UpdatesRecord()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();
        var original = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithSourceType("cli")
            .WithStatus(SessionRecord.Statuses.Created)
            .Build();
        await sut.SaveAsync(original);

        var now = DateTime.UtcNow;
        var updated = original with
        {
            Status = SessionRecord.Statuses.Completed,
            UpdatedAt = now
        };

        // Act
        await sut.SaveAsync(updated);
        var loaded = await sut.LoadAsync("sess-1");

        // Assert
        Expect(loaded).Not.To.Be.Null();
        Expect(loaded!.Status).To.Equal(updated.Status);
        Expect(loaded.UpdatedAt).To.Equal(now);
    }
}

// --- harness (local) ---

file sealed class RepositorySutBuilder
{
    private RepositorySutBuilder() { }

    public static RepositorySutBuilder Create() => new();

    public InMemorySessionRepository Build() => new();
}
