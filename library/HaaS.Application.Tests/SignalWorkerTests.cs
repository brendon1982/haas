using NExpect;
using static NExpect.Expectations;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using NUnit.Framework;
using HaaS.Adapters.Store;

using HaaS.Domain.Tests.Builders;

namespace HaaS.Application.Tests;

[TestFixture]
public class SignalWorkerTests
{
    [Test]
    public async Task ProcessNextAsync_WhenSuccessful_ShouldAck()
    {
        // Arrange
        var queue = new InMemorySignalQueue();
        var signal = new Signal("test", "source");
        await queue.EnqueueAsync(signal, Identity.Anonymous);
        
        var runSessionUseCase = new FakeRunSessionUseCase();
        var resultStore = new FakeDeferredSessionResultStore();
        var logger = new FakeLogger();
        var registry = new FakeSignalSourceRegistry();
        var config = SignalSourceConfigTestBuilder.Create().WithSourceType("source").Build();
        registry.Register(new SignalSourceRegistration(new FakeSignalSource(), new FakeSignalPresenter(), config));

        var sut = new SignalWorker(queue, runSessionUseCase, registry, resultStore, logger);

        // Act
        await sut.ProcessNextAsync(CancellationToken.None);

        // Assert
        var dequeued = await queue.DequeueAsync();
        Expect(dequeued).To.Be.Null(); // Should be removed from queue
    }

    [Test]
    public async Task ProcessNextAsync_WhenFails_ShouldNackWithError()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var queue = new InMemorySignalQueue(timeProvider);
        var signal = new Signal("test", "source");
        await queue.EnqueueAsync(signal, Identity.Anonymous);
        
        var runSessionUseCase = new FakeRunSessionUseCase { ShouldFail = true };
        var resultStore = new FakeDeferredSessionResultStore();
        var logger = new FakeLogger();
        var registry = new FakeSignalSourceRegistry();
        var config = SignalSourceConfigTestBuilder.Create().WithSourceType("source").Build();
        registry.Register(new SignalSourceRegistration(new FakeSignalSource(), new FakeSignalPresenter(), config));

        var sut = new SignalWorker(queue, runSessionUseCase, registry, resultStore, logger);

        // Act & Assert
        Expect(async () => await sut.ProcessNextAsync(CancellationToken.None))
            .To.Throw<Exception>().With.Message.Containing("Simulated failure");

        // Assert queue state
        timeProvider.Advance(TimeSpan.FromSeconds(3)); // 2^1 = 2s
        var dequeued = await queue.DequeueAsync();
        Expect(dequeued).Not.To.Be.Null();
        Expect(dequeued!.RetryCount).To.Equal(1);
        Expect(dequeued.LastError).To.Equal("Simulated failure");
    }

    [Test]
    public async Task ProcessNextAsync_WhenMaxRetriesReached_ShouldMoveToFailed()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var queue = new InMemorySignalQueue(timeProvider);
        var signal = new Signal("test", "source");
        await queue.EnqueueAsync(signal, Identity.Anonymous);
        
        var runSessionUseCase = new FakeRunSessionUseCase { ShouldFail = true };
        var resultStore = new FakeDeferredSessionResultStore();
        var logger = new FakeLogger();
        var registry = new FakeSignalSourceRegistry();
        var config = SignalSourceConfigTestBuilder.Create().WithSourceType("source").Build();
        registry.Register(new SignalSourceRegistration(new FakeSignalSource(), new FakeSignalPresenter(), config));

        var sut = new SignalWorker(queue, runSessionUseCase, registry, resultStore, logger);

        // 1st attempt
        Expect(async () => await sut.ProcessNextAsync(CancellationToken.None)).To.Throw<Exception>();
        
        // 2nd attempt
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        Expect(async () => await sut.ProcessNextAsync(CancellationToken.None)).To.Throw<Exception>();
        
        // 3rd attempt
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        Expect(async () => await sut.ProcessNextAsync(CancellationToken.None)).To.Throw<Exception>();

        // Assert
        var result = await queue.DequeueAsync();
        Expect(result).To.Be.Null(); // Should not be re-enqueued as Pending
    }
}

// --- harness (local) ---

file sealed class FakeRunSessionUseCase : IRunSessionUseCase
{
    public bool ShouldFail { get; set; }
    public Task<SessionResult> ExecuteAsync(Signal signal, ISignalPresenter presenter)
    {
        if (ShouldFail)
        {
            throw new Exception("Simulated failure");
        }
        return Task.FromResult(new SessionResult("", "sess-1"));
    }
}

file sealed class FakeDeferredSessionResultStore : IDeferredSessionResultStore
{
    public void SetResult(string sessionId, SessionResult result) { }
    public void SetError(string sessionId, Exception error) { }
    public Task<SessionResult> WaitForResultAsync(string sessionId, CancellationToken ct = default) 
        => Task.FromResult(new SessionResult("", sessionId));
}

file sealed class FakeSignalSourceRegistry : ISignalSourceRegistry
{
    private readonly Dictionary<string, SignalSourceRegistration> _registrations = new();

    public void Register(SignalSourceRegistration registration)
    {
        _registrations[registration.Config.SourceType] = registration;
    }

    public SignalSourceRegistration? GetBySourceType(string sourceType) 
        => _registrations.TryGetValue(sourceType, out var reg) ? reg : null;

    public IEnumerable<SignalSourceRegistration> GetAll() => _registrations.Values;
}

file sealed class FakeSignalSource : ISignalSource
{
    public string Type => "source";
    public Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler) => Task.CompletedTask;
    public Task ShutdownAsync() => Task.CompletedTask;
}

file sealed class FakeSignalPresenter : ISignalPresenter
{
    public Task PresentAsync(SessionResult result) => Task.CompletedTask;
    public Task PresentErrorAsync(string? sessionId, Exception exception) => Task.CompletedTask;
    public Task PresentProcessingAsync(string sessionId, string? messageId = null) => Task.CompletedTask;
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

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
