using NExpect;
using static NExpect.Expectations;
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
        var signal = SignalTestBuilder.Create()
            .WithPayload("prompt")
            .WithSource("cli")
            .WithSessionId("sess-42")
            .Build();

        // Act
        var result = await sut.ExecuteAsync(signal, "sess-42");

        // Assert
        Expect(result).To.Equal(expected);

        var infoLogs = logger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
        Expect(infoLogs).To.Contain.Exactly(2);

        var startLog = infoLogs[0];
        Expect(startLog.Message).To.Contain("Agent execution started");
        Expect(startLog.Message).To.Contain("sess-42");

        var completeLog = infoLogs[1];
        Expect(completeLog.Message).To.Contain("Agent execution completed");
        Expect(completeLog.Message).To.Contain("sess-42");
        Expect(completeLog.Message).To.Contain("duration");
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
        var signal = SignalTestBuilder.Create()
            .WithPayload("prompt")
            .WithSource("cli")
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, "sess-fail"))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing(expectedError);

        Expect(logger.Logs.Any(l => l.Level == LogLevel.Information
            && l.Message.Contains("Agent execution started"))).To.Be.True();

        var errorLogs = logger.Logs.Where(l => l.Level == LogLevel.Error).ToList();
        Expect(errorLogs).To.Contain.Exactly(1);
        Expect(errorLogs[0].Message).To.Contain("Agent execution failed");
        Expect(errorLogs[0].Message).To.Contain("duration");
        Expect(errorLogs[0].Exception).Not.To.Be.Null();
        Expect(errorLogs[0].Exception!.Message).To.Contain(expectedError);
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
    public Task<SessionResult> ExecuteAsync(SignalValue signal, string sessionId)
        => Task.FromResult(result);
}

file sealed class FailingStrategy(Exception error) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(SignalValue signal, string sessionId)
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
