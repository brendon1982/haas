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
        var signal = SignalTestBuilder.Create()
            .WithSource("cli")
            .Build();
        var sourceConfig = new SignalSourceConfig(
            "cli", "openai", "gpt-4",
            "You are a helpful assistant.", ToolBelt.Empty, "off");
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("hello")
            .WithSessionId("sess-new")
            .Build();
        var strategy = new FakeStrategy(expected);
        var repo = new FakeSessionRepository();
        var configRepo = new FakeSignalSourceConfigRepository();
        await configRepo.SaveAsync(sourceConfig);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithRepository(repo)
            .WithConfigRepository(configRepo)
            .WithTimeProvider(time)
            .Build();

        // Act
        var presenter = new FakePresenter();
        await sut.ExecuteAsync(signal, presenter);

        // Assert
        var record = await repo.LoadAsync(presenter.LastSessionId!);
        Expect(record).Not.To.Be.Null();
        Expect(record!.Status).To.Equal(SessionRecord.Statuses.Completed);
        Expect(record.Provider).To.Equal(sourceConfig.Provider);
        Expect(record.ModelId).To.Equal(sourceConfig.ModelId);
        Expect(record.SystemPrompt).To.Equal(sourceConfig.SystemPrompt);
        Expect(record.SourceType).To.Equal(signal.Source);
        Expect(record.Output).To.Equal(expected.Output);
        Expect(record.CreatedAt).To.Equal(time.UtcNow);
        Expect(record.UpdatedAt).To.Equal(time.UtcNow);
    }

    [Test]
    public async Task Execute_WithExistingSessionId_ContinuesExistingSession()
    {
        // Arrange
        var storedRecord = new SessionRecord(
            "sess-existing", "cli", SessionRecord.Statuses.Running,
            "ollama", "gemma4", "Stored system prompt",
            "[]", "off", null,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var signal = SignalTestBuilder.Create()
            .WithSource("cli")
            .WithSessionId("sess-existing")
            .Build();
        var sourceConfig = new SignalSourceConfig(
            "cli", "openai", "gpt-4",
            "Incoming system prompt", ToolBelt.Empty, "high");
        var expected = SessionResultTestBuilder.Create()
            .WithOutput("continued")
            .WithSessionId("sess-existing")
            .Build();
        var strategy = new FakeStrategy(expected);
        var repo = new FakeSessionRepository();
        await repo.SaveAsync(storedRecord);
        var configRepo = new FakeSignalSourceConfigRepository();
        await configRepo.SaveAsync(sourceConfig);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithRepository(repo)
            .WithConfigRepository(configRepo)
            .WithTimeProvider(time)
            .Build();

        // Act
        var presenter = new FakePresenter();
        await sut.ExecuteAsync(signal, presenter);

        // Assert
        Expect(presenter.LastSessionId).To.Equal("sess-existing");
        var record = await repo.LoadAsync(presenter.LastSessionId!);
        Expect(record).Not.To.Be.Null();
        Expect(record!.Status).To.Equal(SessionRecord.Statuses.Completed);
        Expect(record.Provider).To.Equal("ollama"); // stored config preserved
        Expect(record.ModelId).To.Equal("gemma4");
        Expect(record.SystemPrompt).To.Equal("Stored system prompt");
        Expect(record.UpdatedAt).To.Equal(time.UtcNow);
        Expect(record.CreatedAt).To.Equal(storedRecord.CreatedAt); // unchanged
    }

    [Test]
    public async Task Execute_WhenStrategyThrows_UpdatesStatusToFailed()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithSource("cli")
            .Build();
        var sourceConfig = new SignalSourceConfig(
            "cli", "ollama", "gemma4",
            "You are a helpful assistant.", ToolBelt.Empty, "off");
        var strategy = new FailingStrategy(new InvalidOperationException("fail"));
        var repo = new FakeSessionRepository();
        var configRepo = new FakeSignalSourceConfigRepository();
        await configRepo.SaveAsync(sourceConfig);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithRepository(repo)
            .WithConfigRepository(configRepo)
            .WithTimeProvider(time)
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, new FakePresenter()))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("fail");

        var allRecords = repo.AllRecords();
        Expect(allRecords).To.Contain.Exactly(1);
        Expect(allRecords[0].Status).To.Equal(SessionRecord.Statuses.Failed);
        Expect(allRecords[0].UpdatedAt).To.Equal(time.UtcNow);
    }

    [Test]
    public async Task Execute_WhenNoSourceConfig_Throws()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithSource("unknown")
            .Build();
        var sut = UseCaseSutBuilder.Create().Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(signal, new FakePresenter()))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("unknown");
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
    private ISessionRepository _repository = new FakeSessionRepository();
    private ISignalSourceConfigRepository _configRepository = new FakeSignalSourceConfigRepository();
    private TimeProvider _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

    private UseCaseSutBuilder() { }

    public static UseCaseSutBuilder Create() => new();

    public UseCaseSutBuilder WithStrategy(IAgentStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public UseCaseSutBuilder WithRepository(ISessionRepository repository)
    {
        _repository = repository;
        return this;
    }

    public UseCaseSutBuilder WithConfigRepository(ISignalSourceConfigRepository configRepository)
    {
        _configRepository = configRepository;
        return this;
    }

    public UseCaseSutBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    public RunSessionUseCase Build() => new(_strategy, _repository, _configRepository, _timeProvider);
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
    public async Task<SessionResult> ExecuteAsync(Signal signal, string sessionId, ISignalPresenter presenter)
    {
        var updated = result with { SessionId = sessionId };
        await presenter.PresentAsync(updated);
        return updated;
    }
}

file sealed class FailingStrategy(Exception error) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(Signal signal, string sessionId, ISignalPresenter presenter)
        => throw error;
}

file sealed class FakePresenter : ISignalPresenter
{
    public string? LastSessionId { get; private set; }

    public Task PresentAsync(SessionResult result)
    {
        LastSessionId = result.SessionId;
        return Task.CompletedTask;
    }
}

file sealed class FakeSignalSourceConfigRepository : ISignalSourceConfigRepository
{
    private readonly Dictionary<string, SignalSourceConfig> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<SignalSourceConfig?> GetBySourceTypeAsync(string sourceType)
    {
        _store.TryGetValue(sourceType, out var config);
        return Task.FromResult<SignalSourceConfig?>(config);
    }

    public Task SaveAsync(SignalSourceConfig config)
    {
        _store[config.SourceType] = config;
        return Task.CompletedTask;
    }
}
