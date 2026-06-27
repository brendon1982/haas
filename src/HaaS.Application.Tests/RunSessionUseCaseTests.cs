using NExpect;
using static NExpect.Expectations;
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
        var signal = SignalTestBuilder.Create().Build();
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
        Expect(target.Delivered).To.Equal(expected);
        Expect(sessionId).To.Equal(expected.SessionId);
    }

    [Test]
    public void Execute_WhenStrategyThrows_PropagatesException()
    {
        // Arrange
        var signal = SignalTestBuilder.Create().Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var expectedError = "strategy error";
        var strategy = new FailingStrategy(new InvalidOperationException(expectedError));
        var target = new FakeTarget();
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .Build();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(config, signal));
        Expect(ex!.Message).To.Equal(expectedError);
        Expect(target.Delivered).To.Be.Null();
    }

    [Test]
    public void Execute_WhenTargetThrows_PropagatesException()
    {
        // Arrange
        var signal = SignalTestBuilder.Create().Build();
        var result = SessionResultTestBuilder.Create()
            .WithOutput("ok")
            .WithSessionId("sess-1")
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var strategy = new FakeStrategy(result);
        var expectedError = "delivery error";
        var target = new FailingTarget(new InvalidOperationException(expectedError));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .Build();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(config, signal));
        Expect(ex!.Message).To.Equal(expectedError);
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
