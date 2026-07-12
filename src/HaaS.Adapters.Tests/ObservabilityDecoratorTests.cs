using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Observability;
using HaaS.Domain.Ports;
using HaaS.Application.UseCases;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;
using NSubstitute;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ObservableRunSessionUseCaseTests
{
    [Test]
    public async Task Execute_LogsStartAndCompletion()
    {
        // Arrange
        var inner = Substitute.For<IRunSessionUseCase>();
        inner.ExecuteAsync(Arg.Any<SignalValue>(), Arg.Any<ISignalPresenter>())
             .Returns(Task.FromResult("sess-42"));
        
        var logger = new FakeLogger();
        var sut = new ObservableRunSessionUseCase(inner, logger);
        
        var signal = new SignalValue("cli", "prompt", null);
        var presenter = Substitute.For<ISignalPresenter>();

        // Act
        var result = await sut.ExecuteAsync(signal, presenter);

        // Assert
        Expect(result).To.Equal("sess-42");
        var infoLogs = logger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
        Expect(infoLogs).To.Contain.Exactly(2);
        Expect(infoLogs[0].Message).To.Contain("Session processing started");
        Expect(infoLogs[1].Message).To.Contain("Session processing completed");
        Expect(infoLogs[1].Message).To.Contain("sess-42");
    }

    [Test]
    public void Execute_WhenInnerThrows_LogsErrorAndRethrows()
    {
        // Arrange
        var inner = Substitute.For<IRunSessionUseCase>();
        inner.ExecuteAsync(Arg.Any<SignalValue>(), Arg.Any<ISignalPresenter>())
             .Returns(Task.FromException<string>(new InvalidOperationException("fail")));
        
        var logger = new FakeLogger();
        var sut = new ObservableRunSessionUseCase(inner, logger);
        
        var signal = new SignalValue("cli", "prompt", null);

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, Substitute.For<ISignalPresenter>()))
            .To.Throw<InvalidOperationException>();
        
        var errorLogs = logger.Logs.Where(l => l.Level == LogLevel.Error).ToList();
        Expect(errorLogs).To.Contain.Exactly(1);
        Expect(errorLogs[0].Message).To.Contain("Session processing failed");
    }
}

[TestFixture]
public class ObservableHaasEngineTests
{
    [Test]
    public async Task Run_LogsStartAndStop()
    {
        // Arrange
        var inner = Substitute.For<IHaasEngine>();
        var logger = new FakeLogger();
        var sut = new ObservableHaasEngine(inner, logger);

        // Act
        await sut.RunAsync();

        // Assert
        var infoLogs = logger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
        Expect(infoLogs).To.Contain.Exactly(2);
        Expect(infoLogs[0].Message).To.Contain("HaaS Engine starting");
        Expect(infoLogs[1].Message).To.Contain("HaaS Engine stopped");
    }
}

// Reuse FakeLogger from ObservableAgentStrategyTests or define it here if needed
// For simplicity, I'll copy the minimal parts of FakeLogger and other helpers
// Alternatively, I could move them to a shared file, but project structure 
// seems to prefer local helpers in test files for simplicity in some cases.

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
