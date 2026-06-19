using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Application.Tests;

[TestFixture]
public class RunSessionUseCaseTests
{
    [Test]
    public async Task Execute_WithValidSignal_DeliversSessionResultAndReturnsSessionId()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello")
            .WithSource("test")
            .Build();
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("Hi there!")
            .WithSessionId("sess-1")
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var strategy = new FakeStrategy(expected);
        var target = new FakeTarget();
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .Build();

        // Act
        var sessionId = await sut.ExecuteAsync(config, signal);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(target.Delivered, Is.EqualTo(expected));
            Assert.That(sessionId, Is.EqualTo("sess-1"));
        });
    }

    [Test]
    public void Execute_WhenStrategyThrows_PropagatesException()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello")
            .WithSource("test")
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var strategy = new FailingStrategy(new InvalidOperationException("strategy error"));
        var target = new FakeTarget();
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .Build();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(config, signal));
        Assert.That(ex.Message, Is.EqualTo("strategy error"));
        Assert.That(target.Delivered, Is.Null);
    }

    [Test]
    public void Execute_WhenTargetThrows_PropagatesException()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithPayload("hello")
            .WithSource("test")
            .Build();
        var result = SessionResultTestBuilder.Create()
            .WithOutput("ok")
            .WithSessionId("sess-1")
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var strategy = new FakeStrategy(result);
        var target = new FailingTarget(new InvalidOperationException("delivery error"));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .Build();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(config, signal));
        Assert.That(ex.Message, Is.EqualTo("delivery error"));
    }
}

// --- harness (local) ---

file sealed class UseCaseSutBuilder
{
    private IAgentStrategy _strategy = new FakeStrategy(
        SessionResultTestBuilder.Create()
            .WithOutput("default output")
            .WithSessionId("sess-default")
            .Build());
    private IExecutionTarget _target = new FakeTarget();

    private UseCaseSutBuilder() { }

    public static UseCaseSutBuilder Create() => new();

    public UseCaseSutBuilder WithStrategy(IAgentStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public UseCaseSutBuilder WithTarget(IExecutionTarget target)
    {
        _target = target;
        return this;
    }

    public RunSessionUseCase Build() => new(_strategy, _target);
}

file sealed class FakeStrategy(SessionResult result) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(AgentSessionConfig config, Signal signal)
        => Task.FromResult(result);
}

file sealed class FailingStrategy(Exception error) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(AgentSessionConfig config, Signal signal)
        => throw error;
}

file sealed class FakeTarget : IExecutionTarget
{
    public SessionResult? Delivered { get; private set; }

    public Task DeliverAsync(SessionResult result)
    {
        Delivered = result;
        return Task.CompletedTask;
    }
}

file sealed class FailingTarget(Exception error) : IExecutionTarget
{
    public Task DeliverAsync(SessionResult result) => throw error;
}
