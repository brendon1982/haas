using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Persistence;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class InMemorySessionMessageStoreTests
{
    [Test]
    public async Task GetMessages_ExistingSession_ReturnsMessages()
    {
        // Arrange
        var sut = MessageStoreSutBuilder.Create().Build();
        var sessionId = "sess-1";
        var m1 = new DomainMessage("user", "hello", DateTimeOffset.UtcNow);
        var m2 = new DomainMessage("assistant", "hi", DateTimeOffset.UtcNow);
        await sut.AppendMessagesAsync(sessionId, [m1, m2]);

        // Act
        var result = await sut.GetMessagesAsync(sessionId);

        // Assert
        Expect(result.Count).To.Equal(2);
        Expect(result[0]).To.Equal(m1);
        Expect(result[1]).To.Equal(m2);
    }

    [Test]
    public async Task GetMessages_MissingSession_ReturnsEmpty()
    {
        // Arrange
        var sut = MessageStoreSutBuilder.Create().Build();

        // Act
        var result = await sut.GetMessagesAsync("nonexistent");

        // Assert
        Expect(result.Count).To.Equal(0);
    }

    [Test]
    public async Task AppendMessages_AddsToExistingMessages()
    {
        // Arrange
        var sut = MessageStoreSutBuilder.Create().Build();
        var sessionId = "sess-1";
        var m1 = new DomainMessage("user", "first", DateTimeOffset.UtcNow);
        await sut.AppendMessagesAsync(sessionId, [m1]);

        // Act
        var m2 = new DomainMessage("assistant", "second", DateTimeOffset.UtcNow);
        await sut.AppendMessagesAsync(sessionId, [m2]);
        var result = await sut.GetMessagesAsync(sessionId);

        // Assert
        Expect(result.Count).To.Equal(2);
        Expect(result[0]).To.Equal(m1);
        Expect(result[1]).To.Equal(m2);
    }

    [Test]
    public async Task AppendMessages_NewSession_CreatesMessages()
    {
        // Arrange
        var sut = MessageStoreSutBuilder.Create().Build();
        var m1 = new DomainMessage("user", "first", DateTimeOffset.UtcNow);

        // Act
        await sut.AppendMessagesAsync("new-sess", [m1]);
        var result = await sut.GetMessagesAsync("new-sess");

        // Assert
        Expect(result.Count).To.Equal(1);
        Expect(result[0]).To.Equal(m1);
    }
}

// --- harness (local) ---

file sealed class MessageStoreSutBuilder
{
    private MessageStoreSutBuilder() { }

    public static MessageStoreSutBuilder Create() => new();

    public InMemorySessionMessageStore Build() => new();
}
