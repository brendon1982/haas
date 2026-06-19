using System.Text.Json;
using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class MicrosoftAgentFrameworkStrategyTests
{
    private static readonly AgentSessionConfig DefaultConfig = new(
        "ollama", "gemma4", "You are helpful.", [], "off");

    [Test]
    public async Task Execute_WithoutSessionId_CreatesNewSessionAndPersists()
    {
        // Arrange
        var repo = new InMemorySessionRepository();
        var client = new FakeChatClient("Hello world");
        var strategy = new MicrosoftAgentFrameworkStrategy(client, repo);
        var signal = new SignalValue("hi", "cli");

        // Act
        var result = await strategy.ExecuteAsync(DefaultConfig, signal);

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
        var repo = new InMemorySessionRepository();
        var client = new CapturingChatClient("response");
        var strategy = new MicrosoftAgentFrameworkStrategy(client, repo);

        // First turn - create session
        var signal1 = new SignalValue("first turn", "cli");
        var result1 = await strategy.ExecuteAsync(DefaultConfig, signal1);
        var sessionId = result1.SessionId;

        // Second turn - continue session
        var signal2 = new SignalValue("second turn", "cli", sessionId);

        // Act
        var result2 = await strategy.ExecuteAsync(DefaultConfig, signal2);

        // Assert
        Assert.That(result2.SessionId, Is.EqualTo(sessionId));
        Assert.That(result2.Output, Is.EqualTo("response"));

        // Verify the second call received accumulated messages
        Assert.That(client.ReceivedMessages, Has.Count.EqualTo(2));
        var secondCallMessages = client.ReceivedMessages[1]
            .Select(m => m.Text)
            .Where(t => t != null)
            .ToList();

        // Should contain at least the system prompt and both user messages
        Assert.That(secondCallMessages, Has.Some.Contains("first turn"));
        Assert.That(secondCallMessages, Has.Some.Contains("second turn"));
    }

    [Test]
    public async Task Execute_WithInvalidSessionId_CreatesNewSession()
    {
        // Arrange
        var repo = new InMemorySessionRepository();
        var client = new FakeChatClient("new session");
        var strategy = new MicrosoftAgentFrameworkStrategy(client, repo);
        var signal = new SignalValue("hi", "cli", "nonexistent-id");

        // Act
        var result = await strategy.ExecuteAsync(DefaultConfig, signal);

        // Assert
        Assert.That(result.Output, Is.EqualTo("new session"));
        Assert.That(result.SessionId, Is.Not.EqualTo("nonexistent-id"));
    }

    [Test]
    public async Task Execute_WithCorruptAgentState_CreatesNewSession()
    {
        // Arrange
        var repo = new InMemorySessionRepository();
        var client = new FakeChatClient("recovery");
        var strategy = new MicrosoftAgentFrameworkStrategy(client, repo);

        // Save a corrupt agent state
        var corrupt = new SessionRecord("bad-sess", "cli", "running",
            [0xFF, 0xFE, 0xFD], DateTime.UtcNow, DateTime.UtcNow);
        await repo.SaveAsync(corrupt);

        var signal = new SignalValue("recover", "cli", "bad-sess");

        // Act
        var result = await strategy.ExecuteAsync(DefaultConfig, signal);

        // Assert
        Assert.That(result.Output, Is.EqualTo("recovery"));
        Assert.That(result.SessionId, Is.Not.EqualTo("bad-sess"));
    }
}

// --- harness (local) ---

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
