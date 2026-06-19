using HaaS.Adapters.Store;
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
        var state = new byte[] { 10, 20, 30 };
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithSourceType("cli")
            .WithStatus("running")
            .WithAgentState(state)
            .WithCreatedAt(now)
            .WithUpdatedAt(now)
            .Build();

        // Act
        await sut.SaveAsync(record);
        var loaded = await sut.LoadAsync("sess-1");

        // Assert
        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.SessionId, Is.EqualTo("sess-1"));
            Assert.That(loaded.SourceType, Is.EqualTo("cli"));
            Assert.That(loaded.Status, Is.EqualTo("running"));
            Assert.That(loaded.AgentState, Is.EqualTo(state));
            Assert.That(loaded.CreatedAt, Is.EqualTo(now));
            Assert.That(loaded.UpdatedAt, Is.EqualTo(now));
        });
    }

    [Test]
    public async Task Load_MissingSession_ReturnsNull()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();

        // Act
        var loaded = await sut.LoadAsync("nonexistent");

        // Assert
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task Save_OverwriteExisting_UpdatesRecord()
    {
        // Arrange
        var sut = RepositorySutBuilder.Create().Build();
        var original = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-1")
            .WithSourceType("cli")
            .WithStatus("created")
            .Build();
        await sut.SaveAsync(original);

        var updated = original with
        {
            Status = "completed",
            AgentState = new byte[] { 99 },
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await sut.SaveAsync(updated);
        var loaded = await sut.LoadAsync("sess-1");

        // Assert
        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Status, Is.EqualTo("completed"));
            Assert.That(loaded.AgentState, Is.EqualTo(new byte[] { 99 }));
        });
    }
}

// --- harness (local) ---

file sealed class RepositorySutBuilder
{
    private RepositorySutBuilder() { }

    public static RepositorySutBuilder Create() => new();

    public InMemorySessionRepository Build() => new();
}
