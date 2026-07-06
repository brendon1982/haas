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
        Expect(messages.Any(m => m.Contains("\"hi\""))).To.Be.True();
        Expect(messages.Any(m => m.Contains($"\"{expectedOutput}\""))).To.Be.True();
    }

    [Test]
    public async Task Execute_PassesProviderAndModelIdToFactory()
    {
        // Arrange
        var sessionId = "sess-1";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithProvider("openai")
            .WithModelId("gpt-4")
            .WithSystemPrompt("You are a helpful bot.")
            .WithToolBelt(new ToolBelt(["tool1", "tool2"]))
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
        Expect(factory.LastProvider).To.Equal("openai");
        Expect(factory.LastModelId).To.Equal("gpt-4");
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
        Expect(factory.LastProvider).To.Equal("ollama");
        Expect(factory.LastModelId).To.Equal("gemma4");

        // 2 turns × (1 user + 1 assistant); system prompt seeded by use case, not strategy
        var messages = await messageStore.GetMessagesAsync(sessionId);
        Expect(messages.Count).To.Equal(4);
    }

    [Test]
    public async Task Execute_WithToolBelt_ResolvesToolsFromRegistry()
    {
        // Arrange
        var sessionId = "sess-1";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithSourceType("cli")
            .WithToolBelt(new ToolBelt(["test_tool"]))
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var capturedOptions = new List<ChatOptions?>();
        var chatClient = new CapturingChatOptionsClient("response", capturedOptions);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolRegistry = new FakeToolRegistry();
        toolRegistry.Register("test_tool", (Func<string, Task<string>>)(async input => $"processed: {input}"));
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolRegistry(toolRegistry)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId);

        // Assert
        var lastOptions = capturedOptions.LastOrDefault();
        Expect(lastOptions).Not.To.Be.Null();
        Expect(lastOptions!.Tools).Not.To.Be.Null();
        Expect(lastOptions.Tools.Count).To.Equal(1);
        Expect(lastOptions.Tools[0].Name).To.Equal("test_tool");
    }

    [Test]
    public async Task Execute_WithReplyTool_SetsRequireSpecificMode()
    {
        // Arrange
        var sessionId = "sess-1";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithToolBelt(new ToolBelt(["test_tool"]))
            .WithReplyTool("reply_to_user")
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var capturedOptions = new List<ChatOptions?>();
        var chatClient = new CapturingChatOptionsClient("response", capturedOptions);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolRegistry = new FakeToolRegistry();
        toolRegistry.Register("test_tool", (Func<string, Task<string>>)(async input => $"processed: {input}"));
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolRegistry(toolRegistry)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId);

        // Assert
        var lastOptions = capturedOptions.LastOrDefault();
        Expect(lastOptions).Not.To.Be.Null();
        Expect(lastOptions!.ToolMode).To.Equal(ChatToolMode.RequireSpecific("reply_to_user"));
        Expect(lastOptions.Tools).Not.To.Be.Null();
        Expect(lastOptions.Tools!.Any(t => t.Name == "reply_to_user")).To.Be.False();
    }

    [Test]
    public async Task Execute_WithoutReplyTool_SetsAutoMode()
    {
        // Arrange
        var sessionId = "sess-1";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithToolBelt(new ToolBelt(["test_tool"]))
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var capturedOptions = new List<ChatOptions?>();
        var chatClient = new CapturingChatOptionsClient("response", capturedOptions);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolRegistry = new FakeToolRegistry();
        toolRegistry.Register("test_tool", (Func<string, Task<string>>)(async input => $"processed: {input}"));
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolRegistry(toolRegistry)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId);

        // Assert
        var lastOptions = capturedOptions.LastOrDefault();
        Expect(lastOptions).Not.To.Be.Null();
        Expect(lastOptions!.ToolMode).To.Equal(ChatToolMode.Auto);
    }
}

// --- harness (local) ---

file sealed class StrategySutBuilder
{
    private IChatClientFactory _factory = new FakeChatClientFactory(new FakeChatClient("default response"));
    private InMemorySessionRepository _repository = new();
    private InMemorySessionMessageStore _messageStore = new();
    private IToolRegistry _toolRegistry = new FakeToolRegistry();

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

    public StrategySutBuilder WithToolRegistry(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
        return this;
    }

    public MicrosoftAgentFrameworkStrategy Build() => new(_factory, _repository, _messageStore, _toolRegistry);

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

file sealed class CapturingChatOptionsClient(string response, List<ChatOptions?> captured) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        captured.Add(options);
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
    public string? LastProvider { get; private set; }
    public string? LastModelId { get; private set; }

    public bool CanCreate(string provider) => true;

    public Task<IChatClient> CreateAsync(string provider, string modelId)
    {
        CallCount++;
        LastProvider = provider;
        LastModelId = modelId;
        return Task.FromResult(client);
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
    private readonly ConcurrentDictionary<string, List<string>> _store = new();

    public Task<IReadOnlyList<string>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<string>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<string> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }
}

file sealed class FakeToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, Delegate> _handlers = new();
    private readonly Dictionary<string, string?> _descriptions = new();

    public void Register(string name, Delegate handler, string? description = null)
    {
        _handlers[name] = handler;
        _descriptions[name] = description;
    }

    public IReadOnlyList<AITool> GetTools(IEnumerable<string> toolNames)
    {
        return toolNames
            .Select(name => _handlers.TryGetValue(name, out var handler)
                ? AIFunctionFactory.Create(handler,
                    new AIFunctionFactoryOptions
                    {
                        Name = name,
                        Description = _descriptions.GetValueOrDefault(name)
                    })
                : null)
            .Where(t => t is not null)
            .Cast<AITool>()
            .ToList();
    }
}
