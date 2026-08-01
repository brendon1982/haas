using NExpect;
using static NExpect.Expectations;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Tests.Builders;
using NUnit.Framework;

namespace HaaS.Application.Tests.UseCases;

[TestFixture]
public class EnqueueSignalUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldPreserveContextAndAddMissingSignalMetadata()
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
        var expectedMessageId = "message-42";
        var expectedIdentity = IdentityTestBuilder.Create()
            .WithIssuer("issuer")
            .WithSubject("subject")
            .WithClaim("role", "operator")
            .Build();
        var expectedContext = SignalContextTestBuilder.Create()
            .WithAuthentication(AuthenticationContextTestBuilder.Create()
                .WithIdentity(expectedIdentity)
                .WithAuthenticationMethod("oauth")
                .WithCredentialReference("calendar", "vault", "calendar-ref")
                .Build())
            .WithAttribute("tenant", "contoso")
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(SignalTestBuilder.Create()
                .WithPayload(expectedPayload)
                .WithSource("cli")
                .WithMessageId(expectedMessageId)
                .Build())
            .WithContext(expectedContext)
            .Build();

        // Act
        var sessionId = await sut.ExecuteAsync(envelope);

        // Assert
        Expect(sessionId).Not.To.Be.Null();
        var enqueued = queue.EnqueuedEnvelopes;
        Expect(enqueued).To.Contain.Exactly(1);
        Expect(enqueued[0].Signal.Payload).To.Equal(expectedPayload);
        Expect(enqueued[0].Signal.ArrivedAt).To.Equal(now);
        Expect(enqueued[0].Signal.MessageId).To.Equal(expectedMessageId);
        Expect(enqueued[0].Signal.SessionId).To.Equal(sessionId);
        Expect(enqueued[0].Context).To.Equal(expectedContext);
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
    public List<SignalEnvelope> EnqueuedEnvelopes { get; } = [];

    public Task EnqueueAsync(SignalEnvelope envelope)
    {
        EnqueuedEnvelopes.Add(envelope);
        return Task.CompletedTask;
    }

    public Task<QueuedSignal?> DequeueAsync() => Task.FromResult<QueuedSignal?>(null);
    public Task AckAsync(string signalId) => Task.CompletedTask;
    public Task NackAsync(string signalId, string? error = null) => Task.CompletedTask;
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
