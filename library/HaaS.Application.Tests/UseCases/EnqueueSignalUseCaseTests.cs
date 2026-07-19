using NExpect;
using static NExpect.Expectations;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;

namespace HaaS.Application.Tests.UseCases;

[TestFixture]
public class EnqueueSignalUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldEnqueueSignalWithArrivedAtTimestamp()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var queue = new FakeSignalQueue();
        var timeProvider = new FakeTimeProvider(now);
        var logger = new FakeLogger();
        
        var sut = UseCaseSutBuilder.Create()
            .WithQueue(queue)
            .WithTimeProvider(timeProvider)
            .WithLogger(logger)
            .Build();
        var expectedPayload = "test";
        var signal = new Signal(expectedPayload, "cli");

        // Act
        var sessionId = await sut.ExecuteAsync(signal);

        // Assert
        Expect(sessionId).Not.To.Be.Null();
        var enqueued = queue.EnqueuedSignals;
        Expect(enqueued).To.Contain.Exactly(1);
        var (s, i) = enqueued[0];
        Expect(s.Payload).To.Equal(expectedPayload);
        Expect(s.ArrivedAt).To.Equal(now);
        Expect(s.SessionId).To.Equal(sessionId);
        Expect(i).To.Equal(Identity.Anonymous);
    }
}

// --- harness (local) ---

file sealed class UseCaseSutBuilder
{
    private ISignalQueue _queue = new FakeSignalQueue();
    private TimeProvider _timeProvider = TimeProvider.System;
    private ILogger _logger = new FakeLogger();

    private UseCaseSutBuilder() { }

    public static UseCaseSutBuilder Create() => new();

    public UseCaseSutBuilder WithQueue(ISignalQueue queue)
    {
        _queue = queue;
        return this;
    }

    public UseCaseSutBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    public UseCaseSutBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    public EnqueueSignalUseCase Build() => new(_queue, _timeProvider, _logger);
}

file sealed class FakeSignalQueue : ISignalQueue
{
    public List<(Signal Signal, Identity Identity)> EnqueuedSignals { get; } = [];

    public Task EnqueueAsync(Signal signal, Identity identity)
    {
        EnqueuedSignals.Add((signal, identity));
        return Task.CompletedTask;
    }

    public Task<QueuedSignal?> DequeueAsync() => Task.FromResult<QueuedSignal?>(null);
    public Task AckAsync(string signalId) => Task.CompletedTask;
    public Task NackAsync(string signalId) => Task.CompletedTask;
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

file sealed class FakeLogger : ILogger
{
    public void LogTrace(string message, params object?[] args) { }
    public void LogDebug(string message, params object?[] args) { }
    public void LogInformation(string message, params object?[] args) { }
    public void LogWarning(string message, params object?[] args) { }
    public void LogError(Exception? exception, string message, params object?[] args) { }
    public void LogCritical(Exception? exception, string message, params object?[] args) { }
}
