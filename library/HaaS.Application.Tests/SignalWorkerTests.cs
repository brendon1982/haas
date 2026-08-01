using NExpect;
using static NExpect.Expectations;
using HaaS.Application.UseCases;
using HaaS.Domain.Exceptions;
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
        var expectedContext = SignalContextTestBuilder.Create()
            .WithAuthentication(AuthenticationContextTestBuilder.Create()
                .WithIdentity(IdentityTestBuilder.Create().WithIssuer("issuer").WithSubject("subject").Build())
                .WithAuthenticationMethod("mtls")
                .WithCredentialReference("api", "vault", "api-ref")
                .Build())
            .WithAttribute("tenant", "contoso")
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create().WithPayload("test").WithSource("source").Build())
            .WithContext(expectedContext)
            .Build();
        await queue.EnqueueAsync(envelope);
        
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
        Expect(runSessionUseCase.ReceivedEnvelope?.Context).To.Equal(expectedContext);
    }

    [Test]
    public async Task ProcessNextAsync_WhenFails_ShouldNackWithError()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var queue = new InMemorySignalQueue(timeProvider);
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create().WithPayload("test").WithSource("source").Build())
            .Build();
        await queue.EnqueueAsync(envelope);
        
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
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create().WithPayload("test").WithSource("source").Build())
            .Build();
        await queue.EnqueueAsync(envelope);
        
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

    [Test]
    public async Task ProcessNextAsync_WhenGovernanceIsDenied_PresentsErrorCompletesWaiterAndAcks()
    {
        // Arrange
        var sessionId = "sess-governance-denied";
        var reasonCode = "matched-deny-rule";
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create()
                .WithSource("source")
                .WithSessionId(sessionId)
                .Build())
            .Build();
        var queue = new RecordingSignalQueue();
        await queue.EnqueueAsync(envelope);
        var expectedError = new GovernanceDeniedException(
            sessionId,
            "SessionStart",
            reasonCode,
            "internal-policy-rule");
        var runSessionUseCase = new FakeRunSessionUseCase { Error = expectedError };
        var resultStore = new FakeDeferredSessionResultStore();
        var waiter = resultStore.WaitForResultAsync(sessionId);
        var logger = new FakeLogger();
        var registry = new FakeSignalSourceRegistry();
        var presenter = new FakeSignalPresenter();
        registry.Register(new SignalSourceRegistration(
            new FakeSignalSource(),
            presenter,
            SignalSourceConfigTestBuilder.Create().WithSourceType("source").Build()));
        var sut = new SignalWorker(queue, runSessionUseCase, registry, resultStore, logger);

        // Act
        Expect(async () => await sut.ProcessNextAsync(CancellationToken.None))
            .Not.To.Throw();

        // Assert
        Expect(queue.AcknowledgedIds).To.Contain.Exactly(1);
        Expect(queue.NackedIds).To.Be.Empty();
        Expect(presenter.Errors).To.Contain.Exactly(1);
        Expect(presenter.Errors[0].SessionId).To.Equal(sessionId);
        Expect(presenter.Errors[0].Exception).To.Equal(expectedError);
        Expect(async () => await waiter).To.Throw<GovernanceDeniedException>();
        Expect(logger.WarningMessages.Single()).Not.To.Contain(reasonCode);
        Expect(logger.WarningMessages.Single()).Not.To.Contain("internal-policy-rule");
    }
}

// --- harness (local) ---

file sealed class FakeRunSessionUseCase : IRunSessionUseCase
{
    public bool ShouldFail { get; set; }
    public Exception? Error { get; set; }
    public SignalEnvelope? ReceivedEnvelope { get; private set; }

    public Task<SessionResult> ExecuteAsync(SignalEnvelope envelope, ISignalPresenter presenter)
    {
        ReceivedEnvelope = envelope;
        if (ShouldFail)
        {
            throw new Exception("Simulated failure");
        }
        if (Error is not null)
        {
            throw Error;
        }
        return Task.FromResult(new SessionResult("", "sess-1"));
    }
}

file sealed class FakeDeferredSessionResultStore : IDeferredSessionResultStore
{
    private readonly Dictionary<string, TaskCompletionSource<SessionResult>> _waiters = new();

    public void SetResult(string sessionId, SessionResult result)
    {
        GetWaiter(sessionId).TrySetResult(result);
    }

    public void SetError(string sessionId, Exception error)
    {
        GetWaiter(sessionId).TrySetException(error);
    }

    public Task<SessionResult> WaitForResultAsync(string sessionId, CancellationToken ct = default)
        => GetWaiter(sessionId).Task;

    private TaskCompletionSource<SessionResult> GetWaiter(string sessionId)
    {
        if (!_waiters.TryGetValue(sessionId, out var waiter))
        {
            waiter = new TaskCompletionSource<SessionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters[sessionId] = waiter;
        }

        return waiter;
    }
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
    public List<(string? SessionId, Exception Exception)> Errors { get; } = [];

    public Task PresentAsync(SessionResult result) => Task.CompletedTask;

    public Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        Errors.Add((sessionId, exception));
        return Task.CompletedTask;
    }

    public Task PresentProcessingAsync(string sessionId, string? messageId = null) => Task.CompletedTask;
}

file sealed class FakeLogger : ILogger
{
    public List<string> WarningMessages { get; } = [];

    public void LogTrace(string message, params object?[] args) { }
    public void LogDebug(string message, params object?[] args) { }
    public void LogInformation(string message, params object?[] args) { }
    public void LogWarning(string message, params object?[] args)
    {
        WarningMessages.Add(string.Concat(message, " ", string.Join(" ", args)));
    }

    public void LogError(Exception? exception, string message, params object?[] args) { }
    public void LogCritical(Exception? exception, string message, params object?[] args) { }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

file sealed class RecordingSignalQueue : ISignalQueue
{
    private readonly Queue<QueuedSignal> _queued = new();

    public List<string> AcknowledgedIds { get; } = [];
    public List<string> NackedIds { get; } = [];

    public Task EnqueueAsync(SignalEnvelope envelope)
    {
        _queued.Enqueue(new QueuedSignal(
            Guid.NewGuid().ToString(),
            envelope,
            SignalStatus.Pending,
            DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task<QueuedSignal?> DequeueAsync()
    {
        if (_queued.TryDequeue(out var queued))
        {
            return Task.FromResult<QueuedSignal?>(queued);
        }

        return Task.FromResult<QueuedSignal?>(null);
    }

    public Task AckAsync(string id)
    {
        AcknowledgedIds.Add(id);
        return Task.CompletedTask;
    }

    public Task NackAsync(string id, string? error = null)
    {
        NackedIds.Add(id);
        return Task.CompletedTask;
    }
}
