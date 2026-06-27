using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Domain.Tests.Builders;
using HaaS.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class MicrosoftAgentFrameworkStrategyTests
{
    [Test]
    public async Task Execute_WithoutSessionId_CreatesNewSessionAndPersists()
    {
        // Arrange
        var repo = new InMemorySessionRepository();
        var expectedOutput = "Hello world";
        var sut = StrategySutBuilder.Create()
            .WithClient(new FakeChatClient(expectedOutput))
            .WithRepository(repo)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .WithSource("cli")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(config, signal);

        // Assert
        Expect(result.Output).To.Equal(expectedOutput);
        Expect(result.SessionId).Not.To.Be.Null();
        Expect(result.SessionId).Not.To.Be.Empty();

        var saved = await repo.LoadAsync(result.SessionId);
        Expect(saved).Not.To.Be.Null();
        Expect(saved!.SourceType).To.Equal(signal.Source);
        Expect(saved.Status).To.Equal("running");
        Expect(saved.AgentState).Not.To.Be.Null();
        Expect(saved.AgentState).Not.To.Be.Empty();
    }

    [Test]
    public async Task Execute_WithValidSessionId_ContinuesExistingSession()
    {
        // Arrange
        var expectedResponse = "response";
        var expectedMessageCount = 2;
        var chatClient = new CapturingChatClient(expectedResponse);
        var repo = new InMemorySessionRepository();
        var sut = StrategySutBuilder.Create()
            .WithClient(chatClient)
            .WithRepository(repo)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();

        // First turn - create session
        var signal1 = SignalTestBuilder.Create()
            .WithPayload("first turn")
            .WithSource("cli")
            .Build();
        var result1 = await sut.ExecuteAsync(config, signal1);
        var sessionId = result1.SessionId;

        // Second turn - continue session
        var signal2 = SignalTestBuilder.Create()
            .WithPayload("second turn")
            .WithSource("cli")
            .WithSessionId(sessionId)
            .Build();

        // Act
        var result2 = await sut.ExecuteAsync(config, signal2);

        // Assert
        Expect(result2.SessionId).To.Equal(sessionId);
        Expect(result2.Output).To.Equal(expectedResponse);

        Expect(chatClient.ReceivedMessages).To.Contain.Exactly(expectedMessageCount);
        var secondCallMessages = chatClient.ReceivedMessages[1]
            .Select(m => m.Text)
            .Where(t => t != null)
            .ToList();

        Expect(secondCallMessages).To.Contain.At.Least(1).Matched.By(m => m!.Contains(signal1.Payload));
        Expect(secondCallMessages).To.Contain.At.Least(1).Matched.By(m => m!.Contains(signal2.Payload));
    }

    [Test]
    public async Task Execute_WithInvalidSessionId_CreatesNewSession()
    {
        // Arrange
        var expectedResponse = "new session";
        var sut = StrategySutBuilder.Create()
            .WithClient(new FakeChatClient(expectedResponse))
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("hi")
            .WithSource("cli")
            .WithSessionId("nonexistent-id")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(config, signal);

        // Assert
        Expect(result.Output).To.Equal(expectedResponse);
        Expect(result.SessionId).Not.To.Equal(signal.SessionId);
    }

    [Test]
    public async Task Execute_WithCorruptAgentState_CreatesNewSession()
    {
        // Arrange
        var expectedResponse = "recovery";
        var repo = new InMemorySessionRepository();
        var corrupt = SessionRecordTestBuilder.Create()
            .WithSessionId("bad-sess")
            .WithSourceType("cli")
            .WithStatus("running")
            .WithAgentState([0xFF, 0xFE, 0xFD])
            .Build();
        await repo.SaveAsync(corrupt);

        var sut = StrategySutBuilder.Create()
            .WithClient(new FakeChatClient(expectedResponse))
            .WithRepository(repo)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("recover")
            .WithSource("cli")
            .WithSessionId("bad-sess")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(config, signal);

        // Assert
        Expect(result.Output).To.Equal(expectedResponse);
        Expect(result.SessionId).Not.To.Equal(corrupt.SessionId);
    }
}

// --- harness (local) ---

file sealed class StrategySutBuilder
{
    private IChatClient _client = new FakeChatClient("default response");
    private InMemorySessionRepository _repository = new();

    private StrategySutBuilder() { }

    public static StrategySutBuilder Create() => new();

    public StrategySutBuilder WithClient(IChatClient client)
    {
        _client = client;
        return this;
    }

    public StrategySutBuilder WithRepository(InMemorySessionRepository repository)
    {
        _repository = repository;
        return this;
    }

    public MicrosoftAgentFrameworkStrategy Build() => new(_client, _repository);
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

file sealed class CapturingChatClient(string response) : IChatClient
{
    public List<List<ChatMessage>> ReceivedMessages { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add(messages.ToList());
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
