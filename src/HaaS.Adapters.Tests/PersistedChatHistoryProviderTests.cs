#pragma warning disable MAAI001

using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using HaaS.Adapters.Persistence;
using HaaS.Domain.Ports;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class PersistedChatHistoryProviderTests
{
    [Test]
    public async Task ProvideChatHistoryAsync_WithSessionIdInStateBag_ReturnsMessagesFromStore()
    {
        // Arrange
        var store = new InMemorySessionMessageStore();
        var sessionId = "test-session";
        await store.AppendMessagesAsync(sessionId,
        [
            System.Text.Json.JsonSerializer.Serialize(new ChatMessage(ChatRole.User, "stored question")),
            System.Text.Json.JsonSerializer.Serialize(new ChatMessage(ChatRole.Assistant, "stored answer"))
        ]);

        var sut = new PersistedChatHistoryProvider(store);
        var (agent, session) = await CreateSessionWithAgentAsync(sessionId);
        var context = new ChatHistoryProvider.InvokingContext(
            agent,
            session,
            [new ChatMessage(ChatRole.User, "new question")]);

        // Act
        var result = (await sut.InvokingAsync(context)).ToList();

        // Assert
        Expect(result.Count).To.Equal(3);
        Expect(result[0].Text).To.Equal("stored question");
        Expect(result[1].Text).To.Equal("stored answer");
        Expect(result[2].Text).To.Equal("new question");
    }

    [Test]
    public async Task ProvideChatHistoryAsync_WithoutSessionId_ReturnsOnlyCallerMessages()
    {
        // Arrange
        var store = new InMemorySessionMessageStore();
        var sut = new PersistedChatHistoryProvider(store);
        var (agent, _) = await CreateSessionWithAgentAsync("unused");
        var context = new ChatHistoryProvider.InvokingContext(
            agent,
            null,
            [new ChatMessage(ChatRole.User, "new question")]);

        // Act
        var result = (await sut.InvokingAsync(context)).ToList();

        // Assert
        Expect(result.Count).To.Equal(1);
        Expect(result[0].Text).To.Equal("new question");
    }

    [Test]
    public async Task StoreChatHistoryAsync_AppendsMessagesToStore()
    {
        // Arrange
        var store = new InMemorySessionMessageStore();
        var sessionId = "test-session";
        var (agent, session) = await CreateSessionWithAgentAsync(sessionId);

        var sut = new PersistedChatHistoryProvider(store);
        var context = new ChatHistoryProvider.InvokedContext(
            agent,
            session,
            [new ChatMessage(ChatRole.User, "new question")],
            [new ChatMessage(ChatRole.Assistant, "new answer")]);

        // Act
        await sut.InvokedAsync(context);

        // Assert
        var messages = await store.GetMessagesAsync(sessionId);
        Expect(messages.Count).To.Equal(2);
        var deserialized0 = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(messages[0]);
        var deserialized1 = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(messages[1]);
        Expect(deserialized0!.Text).To.Equal("new question");
        Expect(deserialized1!.Text).To.Equal("new answer");
    }

    [Test]
    public async Task StoreChatHistoryAsync_WithoutSessionId_DoesNothing()
    {
        // Arrange
        var store = new InMemorySessionMessageStore();
        var sut = new PersistedChatHistoryProvider(store);
        var (agent, _) = await CreateSessionWithAgentAsync("unused");
        var context = new ChatHistoryProvider.InvokedContext(
            agent,
            null,
            [new ChatMessage(ChatRole.User, "new question")],
            [new ChatMessage(ChatRole.Assistant, "new answer")]);

        // Act
        await sut.InvokedAsync(context);

        // Assert
        var messages = await store.GetMessagesAsync("any-session");
        Expect(messages.Count).To.Equal(0);
    }

    [Test]
    public void StateKeys_ReturnsDefaultTypeName()
    {
        // Arrange
        var sut = new PersistedChatHistoryProvider(new InMemorySessionMessageStore());

        // Act
        var keys = sut.StateKeys;

        // Assert
        Expect(keys.Count).To.Equal(1);
        Expect(keys[0]).To.Equal("PersistedChatHistoryProvider");
    }

    private static async Task<(AIAgent Agent, AgentSession Session)> CreateSessionWithAgentAsync(string sessionId)
    {
        var client = new StubChatClient();
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions { Name = "test" });
        var session = await agent.CreateSessionAsync();
        session.StateBag.SetValue(PersistedChatHistoryProvider.SessionIdKey, sessionId);
        return (agent, session);
    }
}

// --- harness (local) ---

file sealed class StubChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "stub")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
