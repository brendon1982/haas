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
    public async Task Execute_WithoutSessionId_CreatesSessionRecordAndCompletes()
    {
        // Arrange
        var signal = SignalTestBuilder.Create().Build();
        var config = AgentSessionConfigTestBuilder.Create()
            .WithProvider("openai")
            .WithModelId("gpt-4")
            .Build();
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("hello")
            .WithSessionId("sess-new")
            .Build();
        var strategy = new FakeStrategy(expected);
        var target = new FakeTarget();
        var repo = new FakeSessionRepository();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .WithRepository(repo)
            .WithTimeProvider(time)
            .Build();

        // Act
        var sessionId = await sut.ExecuteAsync(config, signal);

        // Assert
        var record = await repo.LoadAsync(sessionId);
        Expect(record).Not.To.Be.Null();
        Expect(record!.Status).To.Equal("completed");
        Expect(record.Provider).To.Equal(config.Provider);
        Expect(record.ModelId).To.Equal(config.ModelId);
        Expect(record.SystemPrompt).To.Equal(config.SystemPrompt);
        Expect(record.SourceType).To.Equal(signal.Source);
        Expect(record.CreatedAt).To.Equal(time.UtcNow);
        Expect(record.UpdatedAt).To.Equal(time.UtcNow);

        Expect(target.Delivered).Not.To.Be.Null();
        Expect(target.Delivered!.Output).To.Equal(expected.Output);
        Expect(target.Delivered!.SessionId).To.Equal(sessionId);
    }

    [Test]
    public async Task Execute_WithExistingSessionId_ContinuesExistingSession()
    {
        // Arrange
        var storedConfig = AgentSessionConfigTestBuilder.Create()
            .WithModelId("gpt-3.5")
            .Build();
        var storedRecord = new SessionRecord(
            "sess-existing", "cli", "running",
            storedConfig.Provider, storedConfig.ModelId, storedConfig.SystemPrompt,
            "[]", storedConfig.ThinkingLevel,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var signal = SignalTestBuilder.Create()
            .WithSessionId("sess-existing")
            .Build();
        var incomingConfig = AgentSessionConfigTestBuilder.Create()
            .WithModelId("gpt-4") // different from stored
            .Build();
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("continued")
            .WithSessionId("sess-existing")
            .Build();
        var strategy = new FakeStrategy(expected);
        var target = new FakeTarget();
        var repo = new FakeSessionRepository();
        await repo.SaveAsync(storedRecord);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .WithRepository(repo)
            .WithTimeProvider(time)
            .Build();

        // Act
        var sessionId = await sut.ExecuteAsync(incomingConfig, signal);

        // Assert
        Expect(sessionId).To.Equal("sess-existing");
        var record = await repo.LoadAsync(sessionId);
        Expect(record).Not.To.Be.Null();
        Expect(record!.Status).To.Equal("completed");
        Expect(record.ModelId).To.Equal("gpt-3.5"); // stored config preserved
        Expect(record.UpdatedAt).To.Equal(time.UtcNow);
        Expect(record.CreatedAt).To.Equal(storedRecord.CreatedAt); // unchanged
    }

    [Test]
    public async Task Execute_WhenStrategyThrows_UpdatesStatusToFailed()
    {
        // Arrange
        var signal = SignalTestBuilder.Create().Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var strategy = new FailingStrategy(new InvalidOperationException("fail"));
        var target = new FakeTarget();
        var repo = new FakeSessionRepository();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .WithRepository(repo)
            .WithTimeProvider(time)
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(config, signal))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("fail");

        var allRecords = repo.AllRecords();
        Expect(allRecords).To.Contain.Exactly(1);
        Expect(allRecords[0].Status).To.Equal("failed");
        Expect(allRecords[0].UpdatedAt).To.Equal(time.UtcNow);
        Expect(target.Delivered).To.Be.Null();
    }

    [Test]
    public async Task Execute_WhenTargetThrows_KeepsCompletedStatus()
    {
        // Arrange
        var signal = SignalTestBuilder.Create().Build();
        var config = AgentSessionConfigTestBuilder.Create().Build();
        var strategy = new FakeStrategy(
            SessionResultTestBuilder.Create()
                .WithOutput("ok")
                .WithSessionId("sess-1")
                .Build());
        var target = new FailingTarget(new InvalidOperationException("delivery error"));
        var repo = new FakeSessionRepository();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithTarget(target)
            .WithRepository(repo)
            .WithTimeProvider(time)
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(config, signal))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("delivery error");

        // strategy succeeded → status should be "completed" despite delivery failure
        var allRecords = repo.AllRecords();
        Expect(allRecords).To.Contain.Exactly(1);
        Expect(allRecords[0].Status).To.Equal("completed");
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
    private ISessionRepository _repository = new FakeSessionRepository();
    private TimeProvider _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

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

    public UseCaseSutBuilder WithRepository(ISessionRepository repository)
    {
        _repository = repository;
        return this;
    }

    public UseCaseSutBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    public RunSessionUseCase Build() => new(_strategy, _target, _repository, _timeProvider);
}

file sealed class FakeSessionRepository : ISessionRepository
{
    private readonly Dictionary<string, SessionRecord> _store = new();

    public Task SaveAsync(SessionRecord record)
    {
        _store[record.SessionId] = record;
        return Task.CompletedTask;
    }

    public Task<SessionRecord?> LoadAsync(string sessionId)
    {
        _store.TryGetValue(sessionId, out var record);
        return Task.FromResult<SessionRecord?>(record);
    }

    public List<SessionRecord> AllRecords() => [.. _store.Values];
}

file sealed class FakeTimeProvider(DateTimeOffset fixedTime) : TimeProvider
{
    public DateTimeOffset UtcNow => fixedTime;

    public override DateTimeOffset GetUtcNow() => fixedTime;
}

file sealed class FakeStrategy(SessionResult result) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(Signal signal, string sessionId)
        => Task.FromResult(result with { SessionId = sessionId });
}

file sealed class FailingStrategy(Exception error) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(Signal signal, string sessionId)
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
