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
        var expected = new List<ChatMessageData>
        {
            new("user", "hello"),
            new("assistant", "hi")
        };
        await sut.AppendMessagesAsync(sessionId, expected);

        // Act
        var result = await sut.GetMessagesAsync(sessionId);

        // Assert
        Expect(result.Count).To.Equal(2);
        Expect(result[0].Content).To.Equal("hello");
        Expect(result[1].Content).To.Equal("hi");
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
        await sut.AppendMessagesAsync(sessionId, [new ChatMessageData("user", "first")]);

        // Act
        await sut.AppendMessagesAsync(sessionId, [new ChatMessageData("assistant", "second")]);
        var result = await sut.GetMessagesAsync(sessionId);

        // Assert
        Expect(result.Count).To.Equal(2);
        Expect(result[0].Content).To.Equal("first");
        Expect(result[1].Content).To.Equal("second");
    }

    [Test]
    public async Task AppendMessages_NewSession_CreatesMessages()
    {
        // Arrange
        var sut = MessageStoreSutBuilder.Create().Build();

        // Act
        await sut.AppendMessagesAsync("new-sess", [new ChatMessageData("user", "first")]);
        var result = await sut.GetMessagesAsync("new-sess");

        // Assert
        Expect(result.Count).To.Equal(1);
        Expect(result[0].Content).To.Equal("first");
    }
}

// --- harness (local) ---

file sealed class MessageStoreSutBuilder
{
    private MessageStoreSutBuilder() { }

    public static MessageStoreSutBuilder Create() => new();

    public InMemorySessionMessageStore Build() => new();
}
