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
    public async Task Execute_LogsStartAndCompletion()
    {
        // Arrange
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("hello")
            .WithSessionId("sess-42")
            .Build();
        var inner = new FakeStrategy(expected);
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithStrategy(inner)
            .WithLogger(logger)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("prompt")
            .WithSource("cli")
            .WithSessionId("sess-42")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(config, signal);

        // Assert
        Assert.That(result, Is.EqualTo(expected));

        Assert.Multiple(() =>
        {
            var infoLogs = logger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
            Assert.That(infoLogs, Has.Count.EqualTo(2));

            var startLog = infoLogs[0];
            Assert.That(startLog.Message, Does.Contain("Agent execution started"));
            Assert.That(startLog.Message, Does.Contain("sess-42"));

            var completeLog = infoLogs[1];
            Assert.That(completeLog.Message, Does.Contain("Agent execution completed"));
            Assert.That(completeLog.Message, Does.Contain("sess-42"));
            Assert.That(completeLog.Message, Does.Contain("duration"));
        });
    }

    [Test]
    public void Execute_WhenInnerThrows_LogsErrorAndRethrows()
    {
        // Arrange
        var expectedError = "strategy failure";
        var inner = new FailingStrategy(new InvalidOperationException(expectedError));
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithStrategy(inner)
            .WithLogger(logger)
            .Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var signal = SignalTestBuilder.Create()
            .WithPayload("prompt")
            .WithSource("cli")
            .Build();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(config, signal));
        Assert.That(ex!.Message, Is.EqualTo(expectedError));

        Assert.Multiple(() =>
        {
            Assert.That(logger.Logs.Any(l => l.Level == LogLevel.Information
                && l.Message.Contains("Agent execution started")), Is.True);

            var errorLogs = logger.Logs.Where(l => l.Level == LogLevel.Error).ToList();
            Assert.That(errorLogs, Has.Count.EqualTo(1));
            Assert.That(errorLogs[0].Message, Does.Contain("Agent execution failed"));
            Assert.That(errorLogs[0].Message, Does.Contain("duration"));
            Assert.That(errorLogs[0].Exception, Is.Not.Null);
            Assert.That(errorLogs[0].Exception!.Message, Does.Contain(expectedError));
        });
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
    private ILogger _logger = new FakeLogger();

    private SutBuilder() { }

    public static SutBuilder Create() => new();

    public SutBuilder WithStrategy(IAgentStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public SutBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    public ObservableAgentStrategy Build() => new(_strategy, _logger);
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

file sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

file sealed class FakeLogger : ILogger
{
    public List<LogEntry> Logs { get; } = [];

    public void LogTrace(string message, params object?[] args)
        => Logs.Add(new LogEntry(LogLevel.Trace, Format(message, args), null));

    public void LogDebug(string message, params object?[] args)
        => Logs.Add(new LogEntry(LogLevel.Debug, Format(message, args), null));

    public void LogInformation(string message, params object?[] args)
        => Logs.Add(new LogEntry(LogLevel.Information, Format(message, args), null));

    public void LogWarning(string message, params object?[] args)
        => Logs.Add(new LogEntry(LogLevel.Warning, Format(message, args), null));

    public void LogError(Exception? exception, string message, params object?[] args)
        => Logs.Add(new LogEntry(LogLevel.Error, Format(message, args), exception));

    public void LogCritical(Exception? exception, string message, params object?[] args)
        => Logs.Add(new LogEntry(LogLevel.Critical, Format(message, args), exception));

    private static string Format(string message, object?[] args)
        => args.Length > 0 ? string.Format(null, message, args) : message;
}

file enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
