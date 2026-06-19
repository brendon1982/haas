using System.Text.Json;
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
        var sut = StrategySutBuilder.Create()
            .WithClient(new FakeChatClient("Hello world"))
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
        Assert.That(result.Output, Is.EqualTo("Hello world"));
        Assert.That(result.SessionId, Is.Not.Null.And.Not.Empty);

        var saved = await repo.LoadAsync(result.SessionId);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.SourceType, Is.EqualTo("cli"));
        Assert.That(saved.Status, Is.EqualTo("running"));
        Assert.That(saved.AgentState, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Execute_WithValidSessionId_ContinuesExistingSession()
    {
        // Arrange
        var chatClient = new CapturingChatClient("response");
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
        Assert.That(result2.SessionId, Is.EqualTo(sessionId));
        Assert.That(result2.Output, Is.EqualTo("response"));

        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(2));
        var secondCallMessages = chatClient.ReceivedMessages[1]
            .Select(m => m.Text)
            .Where(t => t != null)
            .ToList();

        Assert.That(secondCallMessages, Has.Some.Contains("first turn"));
        Assert.That(secondCallMessages, Has.Some.Contains("second turn"));
    }

    [Test]
    public async Task Execute_WithInvalidSessionId_CreatesNewSession()
    {
        // Arrange
        var sut = StrategySutBuilder.Create()
            .WithClient(new FakeChatClient("new session"))
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
        Assert.That(result.Output, Is.EqualTo("new session"));
        Assert.That(result.SessionId, Is.Not.EqualTo("nonexistent-id"));
    }

    [Test]
    public async Task Execute_WithCorruptAgentState_CreatesNewSession()
    {
        // Arrange
        var repo = new InMemorySessionRepository();
        var corrupt = SessionRecordTestBuilder.Create()
            .WithSessionId("bad-sess")
            .WithSourceType("cli")
            .WithStatus("running")
            .WithAgentState([0xFF, 0xFE, 0xFD])
            .Build();
        await repo.SaveAsync(corrupt);

        var sut = StrategySutBuilder.Create()
            .WithClient(new FakeChatClient("recovery"))
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
        Assert.That(result.Output, Is.EqualTo("recovery"));
        Assert.That(result.SessionId, Is.Not.EqualTo("bad-sess"));
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
