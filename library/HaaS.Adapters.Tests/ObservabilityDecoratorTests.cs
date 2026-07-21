using NExpect;
using static NExpect.Expectations;
using HaaS.Adapters.Observability;
using HaaS.Domain.Ports;
using HaaS.Application.UseCases;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;
using SignalValue = HaaS.Domain.ValueObjects.Signal;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ObservableRunSessionUseCaseTests
{
    [Test]
    public async Task Execute_LogsStartAndCompletion()
    {
        // Arrange
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("hello")
            .WithSessionId("sess-42")
            .Build();
        var inner = new FakeRunSessionUseCase { ResultToReturn = expected };
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithUseCase(inner)
            .WithLogger(logger)
            .BuildUseCase();
        
        var signal = SignalTestBuilder.Create().Build();
        var presenter = new FakePresenter();

        // Act
        var result = await sut.ExecuteAsync(signal, presenter);

        // Assert
        Expect(result).To.Equal(expected);
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
        var inner = new FakeRunSessionUseCase { ErrorToThrow = new InvalidOperationException("fail") };
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithUseCase(inner)
            .WithLogger(logger)
            .BuildUseCase();
        
        var signal = SignalTestBuilder.Create().Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, new FakePresenter()))
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
    public async Task Start_LogsStarting()
    {
        // Arrange
        var inner = new FakeHaasEngine();
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithEngine(inner)
            .WithLogger(logger)
            .BuildEngine();

        // Act
        await sut.StartAsync(default);

        // Assert
        var infoLogs = logger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
        Expect(infoLogs).To.Contain.Exactly(1);
        Expect(infoLogs[0].Message).To.Contain("HaaS Engine starting");
    }

    [Test]
    public async Task Stop_LogsStopping()
    {
        // Arrange
        var inner = new FakeHaasEngine();
        var logger = new FakeLogger();
        var sut = SutBuilder.Create()
            .WithEngine(inner)
            .WithLogger(logger)
            .BuildEngine();

        // Act
        await sut.StopAsync(default);

        // Assert
        var infoLogs = logger.Logs.Where(l => l.Level == LogLevel.Information).ToList();
        Expect(infoLogs).To.Contain.Exactly(1);
        Expect(infoLogs[0].Message).To.Contain("HaaS Engine stopping");
    }
}

// --- harness ---

file sealed class SutBuilder
{
    private IRunSessionUseCase _useCase = new FakeRunSessionUseCase();
    private IHaasEngine _engine = new FakeHaasEngine();
    private ILogger _logger = new FakeLogger();

    public static SutBuilder Create() => new();

    public SutBuilder WithUseCase(IRunSessionUseCase useCase)
    {
        _useCase = useCase;
        return this;
    }

    public SutBuilder WithEngine(IHaasEngine engine)
    {
        _engine = engine;
        return this;
    }

    public SutBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    public ObservableRunSessionUseCase BuildUseCase() => new(_useCase, _logger);
    public ObservableHaasEngine BuildEngine() => new(_engine, _logger);
}

file sealed class FakeRunSessionUseCase : IRunSessionUseCase
{
    public SessionResult? ResultToReturn { get; set; }
    public Exception? ErrorToThrow { get; set; }

    public Task<SessionResult> ExecuteAsync(Signal signal, ISignalPresenter presenter)
    {
        if (ErrorToThrow != null) throw ErrorToThrow;
        return Task.FromResult(ResultToReturn ?? SessionResultTestBuilder.Create().Build());
    }
}

file sealed class FakeHaasEngine : IHaasEngine
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

file sealed class FakePresenter : ISignalPresenter
{
    public Task PresentProcessingAsync(string sessionId) => Task.CompletedTask;
    public Task PresentAsync(SessionResult result) => Task.CompletedTask;
    public Task PresentErrorAsync(string? sessionId, Exception exception) => Task.CompletedTask;
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
