using System.Collections.Concurrent;
using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class MicrosoftAgentFrameworkStrategyTests
{
    [Test]
    public async Task Execute_LoadsConfigFromRecordAndReturnsResult()
    {
        // Arrange
        var expectedOutput = "Hello world";
        var sessionId = "sess-1";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithSourceType("cli")
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var chatClient = new FakeChatClient(expectedOutput);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .WithSource("cli")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(signal, sessionId);

        // Assert
        Expect(result.Output).To.Equal(expectedOutput);
        Expect(result.SessionId).To.Equal(sessionId);

        var messages = await messageStore.GetMessagesAsync(sessionId);
        Expect(messages.Count).Not.To.Equal(0);
        var texts = messages.Select(m => m.Content).ToList();
        Expect(texts).To.Contain("hi");
        Expect(texts).To.Contain(expectedOutput);
    }

    [Test]
    public async Task Execute_PassesConfigToFactory()
    {
        // Arrange
        var sessionId = "sess-1";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithProvider("openai")
            .WithModelId("gpt-4")
            .WithSystemPrompt("You are a helpful bot.")
            .WithTools("[\"tool1\",\"tool2\"]")
            .WithThinkingLevel("high")
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var chatClient = new FakeChatClient("response");
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId);

        // Assert
        Expect(factory.LastConfig).Not.To.Be.Null();
        Expect(factory.LastConfig!.Provider).To.Equal("openai");
        Expect(factory.LastConfig!.ModelId).To.Equal("gpt-4");
        Expect(factory.LastConfig!.SystemPrompt).To.Equal("You are a helpful bot.");
        Expect(factory.LastConfig!.Tools.Count).To.Equal(2);
        Expect(factory.LastConfig!.ThinkingLevel).To.Equal("high");
    }

    [Test]
    public async Task Execute_WhenRecordNotFound_Throws()
    {
        // Arrange
        var chatClient = new FakeChatClient("response");
        var factory = new FakeChatClientFactory(chatClient);
        var repo = new InMemorySessionRepository();
        var messageStore = new InMemorySessionMessageStore();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, "nonexistent"))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("nonexistent");
    }

    [Test]
    public async Task Execute_WithSystemPrompt_PrependsSystemMessage()
    {
        // Arrange
        var sessionId = "sess-1";
        var systemPrompt = "You are a helpful assistant.";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithSystemPrompt(systemPrompt)
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);

        var capturedMessages = new List<ChatMessage>();
        var chatClient = new CapturingChatClient("response", capturedMessages);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("user message")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId);

        // Assert
        var firstMessage = capturedMessages.First();
        Expect(firstMessage.Role.ToString()).To.Equal("system");
        Expect(firstMessage.Text).To.Equal(systemPrompt);
    }

    [Test]
    public async Task Execute_WithTwoTurns_LoadsSameConfig()
    {
        // Arrange
        var expectedResponse = "response";
        var sessionId = "sess-multi";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithProvider("ollama")
            .WithModelId("gemma4")
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var chatClient = new FakeChatClient(expectedResponse);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .Build();

        // Act - first turn
        var signal1 = SignalTestBuilder.Create()
            .WithPayload("first turn")
            .Build();
        var result1 = await sut.ExecuteAsync(signal1, sessionId);

        // Act - second turn
        var signal2 = SignalTestBuilder.Create()
            .WithPayload("second turn")
            .Build();
        var result2 = await sut.ExecuteAsync(signal2, sessionId);

        // Assert
        Expect(result1.SessionId).To.Equal(sessionId);
        Expect(result2.SessionId).To.Equal(sessionId);
        Expect(factory.CallCount).To.Equal(2);
        Expect(factory.LastConfig!.Provider).To.Equal("ollama");
        Expect(factory.LastConfig!.ModelId).To.Equal("gemma4");

        // 2 turns × (1 system + 1 user + 1 assistant)
        var messages = await messageStore.GetMessagesAsync(sessionId);
        Expect(messages.Count).To.Equal(6);
    }
}

// --- harness (local) ---

file sealed class StrategySutBuilder
{
    private IChatClientFactory _factory = new FakeChatClientFactory(new FakeChatClient("default response"));
    private InMemorySessionRepository _repository = new();
    private InMemorySessionMessageStore _messageStore = new();

    private StrategySutBuilder() { }

    public static StrategySutBuilder Create() => new();

    public StrategySutBuilder WithChatClientFactory(IChatClientFactory factory)
    {
        _factory = factory;
        return this;
    }

    public StrategySutBuilder WithRepository(InMemorySessionRepository repository)
    {
        _repository = repository;
        return this;
    }

    public StrategySutBuilder WithMessageStore(InMemorySessionMessageStore messageStore)
    {
        _messageStore = messageStore;
        return this;
    }

    public MicrosoftAgentFrameworkStrategy Build() => new(_factory, _repository, _messageStore);

    public InMemorySessionMessageStore MessageStore => _messageStore;
}

file sealed class FakeChatClient(string response) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
        return Task.FromResult(chatResponse);
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

file sealed class CapturingChatClient(string response, List<ChatMessage> captured) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        captured.Clear();
        captured.AddRange(messages);
        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
        return Task.FromResult(chatResponse);
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

file sealed class FakeChatClientFactory(IChatClient client) : IChatClientFactory
{
    public int CallCount { get; private set; }
    public AgentSessionConfig? LastConfig { get; private set; }

    public IChatClient Create(AgentSessionConfig config)
    {
        CallCount++;
        LastConfig = config;
        return client;
    }
}

file sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly Dictionary<string, SessionRecord> _store = new();

    public Task SaveAsync(SessionRecord record)
    {
        _store[record.SessionId] = record;
        return Task.CompletedTask;
    }

    public Task<SessionRecord?> LoadAsync(string sessionId)
    {
        _store.TryGetValue(sessionId, out var record);
        return Task.FromResult<SessionRecord?>(record);
    }
}

file sealed class InMemorySessionMessageStore : IMessageStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessageData>> _store = new();

    public Task<IReadOnlyList<ChatMessageData>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<ChatMessageData>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<ChatMessageData>>(Array.Empty<ChatMessageData>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<ChatMessageData> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }
}
