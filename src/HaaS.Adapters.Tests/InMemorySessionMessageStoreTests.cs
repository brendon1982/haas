using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Persistence;
using Microsoft.Extensions.AI;
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
        var expected = new List<ChatMessage>
        {
            new(ChatRole.User, "hello"),
            new(ChatRole.Assistant, "hi")
        };
        await sut.AppendMessagesAsync(sessionId, expected);

        // Act
        var result = await sut.GetMessagesAsync(sessionId);

        // Assert
        Expect(result.Count).To.Equal(2);
        Expect(result[0].Text).To.Equal("hello");
        Expect(result[1].Text).To.Equal("hi");
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
        await sut.AppendMessagesAsync(sessionId, [new ChatMessage(ChatRole.User, "first")]);

        // Act
        await sut.AppendMessagesAsync(sessionId, [new ChatMessage(ChatRole.Assistant, "second")]);
        var result = await sut.GetMessagesAsync(sessionId);

        // Assert
        Expect(result.Count).To.Equal(2);
        Expect(result[0].Text).To.Equal("first");
        Expect(result[1].Text).To.Equal("second");
    }

    [Test]
    public async Task AppendMessages_NewSession_CreatesMessages()
    {
        // Arrange
        var sut = MessageStoreSutBuilder.Create().Build();

        // Act
        await sut.AppendMessagesAsync("new-sess", [new ChatMessage(ChatRole.User, "first")]);
        var result = await sut.GetMessagesAsync("new-sess");

        // Assert
        Expect(result.Count).To.Equal(1);
        Expect(result[0].Text).To.Equal("first");
    }
}

// --- harness (local) ---

file sealed class MessageStoreSutBuilder
{
    private MessageStoreSutBuilder() { }

    public static MessageStoreSutBuilder Create() => new();

    public InMemorySessionMessageStore Build() => new();
}
