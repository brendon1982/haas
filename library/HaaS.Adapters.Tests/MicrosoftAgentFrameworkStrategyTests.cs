using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
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
        var toolProvider = new FakeToolProvider();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .WithSource("cli")
            .Build();

        // Act
        var presenter = new RecordingPresenter();
        await sut.ExecuteAsync(signal, sessionId, presenter);

        // Assert
        Expect(presenter.Results).To.Contain.Exactly(1);
        Expect(presenter.Results[0].Output).To.Equal(expectedOutput);
        Expect(presenter.Results[0].SessionId).To.Equal(sessionId);

        var messages = await messageStore.GetMessagesAsync(sessionId);
        Expect(messages.Count).Not.To.Equal(0);
        Expect(messages.Any(m => m.Content == "hi")).To.Be.True();
        Expect(messages.Any(m => m.Content == expectedOutput)).To.Be.True();
    }

    [Test]
    public async Task Execute_SeedsSystemPromptOnNewSession()
    {
        // Arrange
        var sessionId = "sess-new";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithSystemPrompt("You are a helpful bot.")
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var chatClient = new FakeChatClient("response");
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolProvider = new FakeToolProvider();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId, new RecordingPresenter());

        // Assert
        var messages = await messageStore.GetMessagesAsync(sessionId);
        var systemMessage = messages[0];
        Expect(systemMessage.Role).To.Equal("system");
        Expect(systemMessage.Content).To.Equal(record.SystemPrompt);
    }

    [Test]
    public async Task Execute_PassesProviderAndModelIdToFactory()
    {
        // Arrange
        var sessionId = "sess-1";
        var expectedProvider = "openai";
        var expectedModelId = "gpt-4";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithProvider(expectedProvider)
            .WithModelId(expectedModelId)
            .WithSystemPrompt("You are a helpful bot.")
            .WithToolBelt(new ToolBelt(["tool1", "tool2"]))
            .WithThinkingLevel("high")
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var chatClient = new FakeChatClient("response");
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolProvider = new FakeToolProvider();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId, new RecordingPresenter());

        // Assert
        Expect(factory.LastProvider).To.Equal(expectedProvider);
        Expect(factory.LastModelId).To.Equal(expectedModelId);
    }

    [Test]
    public async Task Execute_WhenRecordNotFound_Throws()
    {
        // Arrange
        var chatClient = new FakeChatClient("response");
        var factory = new FakeChatClientFactory(chatClient);
        var repo = new InMemorySessionRepository();
        var messageStore = new InMemorySessionMessageStore();
        var toolProvider = new FakeToolProvider();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, "nonexistent", new RecordingPresenter()))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("nonexistent");
    }

    [Test]
    public async Task Execute_WithTwoTurns_LoadsSameConfig()
    {
        // Arrange
        var expectedResponse = "response";
        var expectedProvider = "ollama";
        var expectedModelId = "gemma4";
        var sessionId = "sess-multi";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithProvider(expectedProvider)
            .WithModelId(expectedModelId)
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var chatClient = new FakeChatClient(expectedResponse);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolProvider = new FakeToolProvider();
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();

        // Act - first turn
        var presenter = new RecordingPresenter();
        var signal1 = SignalTestBuilder.Create()
            .WithPayload("first turn")
            .Build();
        await sut.ExecuteAsync(signal1, sessionId, presenter);

        // Act - second turn
        var signal2 = SignalTestBuilder.Create()
            .WithPayload("second turn")
            .Build();
        await sut.ExecuteAsync(signal2, sessionId, presenter);

        // Assert
        Expect(presenter.Results).To.Contain.Exactly(2);
        Expect(presenter.Results[0].SessionId).To.Equal(sessionId);
        Expect(presenter.Results[1].SessionId).To.Equal(sessionId);
        Expect(presenter.Results[0].Output).To.Equal(expectedResponse);
        Expect(presenter.Results[1].Output).To.Equal(expectedResponse);
        Expect(factory.CallCount).To.Equal(2);
        Expect(factory.LastProvider).To.Equal(expectedProvider);
        Expect(factory.LastModelId).To.Equal(expectedModelId);

        // system prompt seeded once + 2 turns × (1 user + 1 assistant)
        var messages = await messageStore.GetMessagesAsync(sessionId);
        Expect(messages.Count).To.Equal(5);
    }

    [Test]
    public async Task Execute_WithToolBelt_ResolvesToolsFromRegistry()
    {
        // Arrange
        var sessionId = "sess-1";
        var expectedTool = "test_tool";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithSourceType("cli")
            .WithToolBelt(new ToolBelt([expectedTool]))
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);
        var capturedOptions = new List<ChatOptions?>();
        var chatClient = new CapturingChatOptionsClient("response", capturedOptions);
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        var toolProvider = new FakeToolProvider();
        toolProvider.Register(new ToolDefinition(expectedTool, "", (Func<string, Task<string>>)(async input => $"processed: {input}")));
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act
        await sut.ExecuteAsync(signal, sessionId, new RecordingPresenter());

        // Assert
        var lastOptions = capturedOptions.LastOrDefault();
        Expect(lastOptions).Not.To.Be.Null();
        Expect(lastOptions!.Tools).Not.To.Be.Null();
        Expect(lastOptions.Tools!.Count).To.Equal(1);
        Expect(lastOptions.Tools[0].Name).To.Equal(expectedTool);
        Expect(lastOptions.ToolMode).To.Be.Null();
    }

    [Test]
    public async Task Execute_WithInstanceMethod_ShouldNotThrowTargetException()
    {
        // Arrange
        var sessionId = "sess-instance-method";
        var toolName = "format";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithToolBelt(new ToolBelt([toolName]))
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);

        var myTool = new MyTool();
        var services = new ServiceCollection();
        services.AddSingleton(myTool);
        var sp = services.BuildServiceProvider();
        var scopeAccessor = new InternalFakeScopeAccessor { ServiceProvider = sp };
        var toolProvider = new ToolProvider(scopeAccessor);

        // Register using the generic method which uses Expression and ExtractMethodInfo
        toolProvider.Register<MyTool>(toolName, "description", t => (Func<string, Task<string>>)t.ExecuteAsync);

        var factory = new FakeChatClientFactory(new ToolCallingFakeChatClient(toolName, "input-value"));
        var messageStore = new InMemorySessionMessageStore();
        
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();

        var signal = SignalTestBuilder.Create()
            .WithPayload("run tool")
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, sessionId, new RecordingPresenter()))
            .Not.To.Throw();
    }

    [Test]
    public async Task Execute_WithToolRegisteredViaGeneric_ShouldNotThrow_WhenCreatingAIFunction()
    {
        // Arrange
        var sessionId = "sess-generic-tool";
        var toolName = "greet";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithToolBelt(new ToolBelt([toolName]))
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);

        var services = new ServiceCollection();
        services.AddSingleton<MyTool>();
        var sp = services.BuildServiceProvider();
        var scopeAccessor = new InternalFakeScopeAccessor { ServiceProvider = sp };
        var toolProvider = new ToolProvider(scopeAccessor);

        // This uses the generic Register<T> which builds an Expression-based wrapper
        // The issue is likely here: the wrapper's parameters might lose their names.
        toolProvider.Register<MyTool>(toolName, "description", t => (Func<string, Task<string>>)t.ExecuteAsync);

        var factory = new FakeChatClientFactory(new ToolCallingFakeChatClient(toolName, "world"));
        var messageStore = new InMemorySessionMessageStore();
        
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();

        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, sessionId, new RecordingPresenter()))
            .Not.To.Throw();
    }

    [Test]
    public async Task Execute_WithToolFromProvider_ShouldNotThrowTargetException_WhenInvoked()
    {
        // Arrange
        var sessionId = "sess-tool-invocation";
        var toolName = "my_tool";
        var record = SessionRecordTestBuilder.Create()
            .WithSessionId(sessionId)
            .WithToolBelt(new ToolBelt([toolName]))
            .Build();
        var repo = new InMemorySessionRepository();
        await repo.SaveAsync(record);

        var services = new ServiceCollection();
        services.AddSingleton<MyTool>();
        var sp = services.BuildServiceProvider();
        var scopeAccessor = new InternalFakeScopeAccessor { ServiceProvider = sp };
        var toolProvider = new ToolProvider(scopeAccessor);

        // Register manually to ensure it works without MethodInfo
        toolProvider.Register(new ToolDefinition(toolName, "description", (Func<string, Task<string>>)(input => Task.FromResult($"result: {input}"))));

        // We need a ChatClient that will actually call the tool
        var chatClient = new ToolCallingFakeChatClient(toolName, "arg1");
        var factory = new FakeChatClientFactory(chatClient);
        var messageStore = new InMemorySessionMessageStore();
        
        var sut = StrategySutBuilder.Create()
            .WithChatClientFactory(factory)
            .WithRepository(repo)
            .WithMessageStore(messageStore)
            .WithToolProvider(toolProvider)
            .Build();

        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, sessionId, new RecordingPresenter()))
            .Not.To.Throw();
    }

    public class MyTool
    {
        public Task<string> ExecuteAsync(string input) => Task.FromResult($"result: {input}");
    }

    private class InternalFakeScopeAccessor : ISignalScopeAccessor
    {
        public IServiceProvider ServiceProvider { get; set; }
    }
}

// --- harness (local) ---

file sealed class ToolCallingFakeChatClient(string toolName, string toolArg) : IChatClient
{
    private bool _called = false;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!_called)
        {
            _called = true;
            var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, [
                new FunctionCallContent("call1", toolName, new Dictionary<string, object?> { ["input"] = toolArg })
            ]));
            return Task.FromResult(chatResponse);
        }
        else
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Final response")));
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

file sealed class RecordingPresenter : ISignalPresenter
{
    public List<SessionResult> Results { get; } = [];

    public Task PresentProcessingAsync(string sessionId, string? messageId = null) => Task.CompletedTask;

    public Task PresentAsync(SessionResult result)
    {
        Results.Add(result);
        return Task.CompletedTask;
    }

    public Task PresentErrorAsync(string? sessionId, Exception exception) => Task.CompletedTask;
}

file sealed class StrategySutBuilder
{
    private IChatClientFactory _factory = new FakeChatClientFactory(new FakeChatClient("default response"));
    private InMemorySessionRepository _repository = new();
    private IMessageStore _messageStore = new InMemorySessionMessageStore();
    private IToolProvider _toolProvider = new FakeToolProvider();
    private TimeProvider _timeProvider = TimeProvider.System;

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

    public StrategySutBuilder WithMessageStore(IMessageStore messageStore)
    {
        _messageStore = messageStore;
        return this;
    }

    public StrategySutBuilder WithToolProvider(IToolProvider toolProvider)
    {
        _toolProvider = toolProvider;
        return this;
    }

    public StrategySutBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    public MicrosoftAgentFrameworkStrategy Build() => new(_factory, _repository, _messageStore, _toolProvider, _timeProvider);

    public IMessageStore MessageStore => _messageStore;
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

    public void ConfigureOptions(string provider, ChatOptions options, AgentSessionConfig config) { }
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
    private readonly ConcurrentDictionary<string, List<DomainMessage>> _store = new();

    public Task<IReadOnlyList<DomainMessage>> GetMessagesAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<DomainMessage>>(messages.ToList());
        }

        return Task.FromResult<IReadOnlyList<DomainMessage>>(Array.Empty<DomainMessage>());
    }

    public Task AppendMessagesAsync(string sessionId, IEnumerable<DomainMessage> messages)
    {
        var list = _store.GetOrAdd(sessionId, _ => []);
        list.AddRange(messages);
        return Task.CompletedTask;
    }

    public Task<int> GetMessageCountAsync(string sessionId)
    {
        if (_store.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult(messages.Count);
        }

        return Task.FromResult(0);
    }
}

file sealed class FakeToolProvider : IToolProvider
{
    private readonly Dictionary<string, ToolDefinition> _tools = new();

    public void Register(ToolDefinition definition)
    {
        _tools[definition.Name] = definition;
    }

    public void Register<T>(string name, string description, Expression<Func<T, Delegate>> methodSelector) where T : class
    {
    }

    public IEnumerable<ToolDefinition> GetTools(IEnumerable<string> toolNames)
    {
        return toolNames
            .Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
            .Where(t => t is not null)!;
    }
}
