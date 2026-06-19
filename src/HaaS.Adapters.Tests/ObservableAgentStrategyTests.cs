using HaaS.Adapters.Observability;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ObservableAgentStrategyTests
{
    [Test]
    public async Task Execute_RecordsMetricsAndEventOnSuccess()
    {
        // Arrange
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("hello")
            .WithSessionId("sess-42")
            .Build();
        var inner = new FakeStrategy(expected);
        var telemetry = new FakeTelemetry();
        var sut = SutBuilder.Create()
            .WithStrategy(inner)
            .WithTelemetry(telemetry)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("prompt")
            .WithSource("cli")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(config, signal);

        // Assert
        Assert.That(result, Is.EqualTo(expected));

        Assert.Multiple(() =>
        {
            var startMetric = telemetry.Metrics.FirstOrDefault(m => m.Name == "agent.execute.start");
            Assert.That(startMetric.Name, Is.EqualTo("agent.execute.start"));
            Assert.That(startMetric.Value, Is.EqualTo(1));

            var durationMetric = telemetry.Metrics.FirstOrDefault(m => m.Name == "agent.execute.duration_ms");
            Assert.That(durationMetric.Name, Is.EqualTo("agent.execute.duration_ms"));
            Assert.That(durationMetric.Value, Is.GreaterThanOrEqualTo(0));

            Assert.That(telemetry.Events, Has.Count.EqualTo(1));
            var evt = telemetry.Events[0];
            Assert.That(evt.SessionId, Is.EqualTo(expected.SessionId));
            Assert.That(evt.Phase, Is.EqualTo("complete"));
            Assert.That(evt.Input, Is.EqualTo(signal.Payload));
            Assert.That(evt.Output, Is.EqualTo(expected.Output));
        });
    }

    [Test]
    public void Execute_WhenInnerThrows_RecordsErrorMetricAndRethrows()
    {
        // Arrange
        var expectedError = "strategy failure";
        var inner = new FailingStrategy(new InvalidOperationException(expectedError));
        var telemetry = new FakeTelemetry();
        var sut = SutBuilder.Create()
            .WithStrategy(inner)
            .WithTelemetry(telemetry)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("prompt")
            .WithSource("cli")
            .Build();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(config, signal));
        Assert.That(ex.Message, Is.EqualTo(expectedError));

        var errorMetric = telemetry.Metrics.FirstOrDefault(m => m.Name == "agent.execute.error");
        Assert.That(errorMetric.Name, Is.EqualTo("agent.execute.error"));
        Assert.That(errorMetric.Value, Is.EqualTo(1));

        Assert.That(telemetry.Metrics.Any(m => m.Name == "agent.execute.start"), Is.True);
        Assert.That(telemetry.Events, Is.Empty);
    }
}

// --- harness (local) ---

file sealed class SutBuilder
{
    private IAgentStrategy _strategy = new FakeStrategy(
        SessionResultTestBuilder.Create()
            .WithOutput("default output")
            .WithSessionId("sess-default")
            .Build());
    private IObservabilityProvider _telemetry = new FakeTelemetry();

    private SutBuilder() { }

    public static SutBuilder Create() => new();

    public SutBuilder WithStrategy(IAgentStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public SutBuilder WithTelemetry(IObservabilityProvider telemetry)
    {
        _telemetry = telemetry;
        return this;
    }

    public ObservableAgentStrategy Build() => new(_strategy, _telemetry);
}

file sealed class FakeStrategy(SessionResult result) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(AgentSessionConfig config, SignalValue signal)
        => Task.FromResult(result);
}

file sealed class FailingStrategy(Exception error) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(AgentSessionConfig config, SignalValue signal)
        => throw error;
}

file sealed class FakeTelemetry : IObservabilityProvider
{
    public List<(string Name, double Value)> Metrics { get; } = [];
    public List<AgentIterationEvent> Events { get; } = [];

    public Task RecordMetricAsync(string name, double value)
    {
        Metrics.Add((name, value));
        return Task.CompletedTask;
    }

    public Task RecordEventAsync(AgentIterationEvent evt)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
}
