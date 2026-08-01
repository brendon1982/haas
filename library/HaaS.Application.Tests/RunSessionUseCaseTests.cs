using NExpect;
using static NExpect.Expectations;
using HaaS.Application.UseCases;
using HaaS.Domain.Exceptions;
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
        var envelope = SignalEnvelopeTestBuilder.Create().WithSignal(signal).Build();
        var sourceConfig = SignalSourceConfigTestBuilder.Create()
            .WithSourceType("cli")
            .WithProvider("openai")
            .WithModelId("gpt-4")
            .WithSystemPrompt("You are a helpful assistant.")
            .WithToolBelt(ToolBelt.Empty)
            .WithObservabilityMode("off")
            .Build();
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
        await sut.ExecuteAsync(envelope, presenter);

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
        var storedRecord = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-existing")
            .WithSourceType("cli")
            .WithIdentityIssuer("test-issuer")
            .WithIdentitySubject("test-subject")
            .WithStatus(SessionRecord.Statuses.Running)
            .WithProvider("ollama")
            .WithModelId("gemma4")
            .WithSystemPrompt("Stored system prompt")
            .WithToolBelt(ToolBelt.Empty)
            .WithThinkingLevel("off")
            .WithCreatedAt(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .WithUpdatedAt(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithSource("cli")
            .WithSessionId("sess-existing")
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create().WithSignal(signal).Build();
        var sourceConfig = SignalSourceConfigTestBuilder.Create()
            .WithSourceType("cli")
            .WithProvider("openai")
            .WithModelId("gpt-4")
            .WithSystemPrompt("Incoming system prompt")
            .WithToolBelt(ToolBelt.Empty)
            .WithObservabilityMode("high")
            .Build();
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
        await sut.ExecuteAsync(envelope, presenter);

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
    public async Task Execute_WithExistingSessionAndDifferentSource_RejectsWithoutMutatingSession()
    {
        // Arrange
        var storedRecord = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-existing")
            .WithSourceType("cli")
            .WithIdentityIssuer("issuer")
            .WithIdentitySubject("subject")
            .WithStatus(SessionRecord.Statuses.Completed)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithSource("web")
            .WithSessionId(storedRecord.SessionId)
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(signal)
            .WithContext(SignalContextTestBuilder.Create()
                .WithAuthentication(AuthenticationContextTestBuilder.Create()
                    .WithIdentity(IdentityTestBuilder.Create()
                        .WithIssuer(storedRecord.IdentityIssuer)
                        .WithSubject(storedRecord.IdentitySubject)
                        .Build())
                    .Build())
                .Build())
            .Build();
        var repository = new FakeSessionRepository();
        await repository.SaveAsync(storedRecord);
        var configRepository = new FakeSignalSourceConfigRepository();
        await configRepository.SaveAsync(SignalSourceConfigTestBuilder.Create()
            .WithSourceType(signal.Source)
            .Build());
        var sut = UseCaseSutBuilder.Create()
            .WithRepository(repository)
            .WithConfigRepository(configRepository)
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(envelope, new FakePresenter()))
            .To.Throw<GovernanceDeniedException>();
        Expect(await repository.LoadAsync(storedRecord.SessionId)).To.Equal(storedRecord);
    }

    [Test]
    public async Task Execute_WithExistingSessionAndDifferentIdentity_RejectsWithoutMutatingSession()
    {
        // Arrange
        var storedRecord = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-existing")
            .WithSourceType("cli")
            .WithIdentityIssuer("issuer")
            .WithIdentitySubject("original-subject")
            .WithStatus(SessionRecord.Statuses.Completed)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithSource(storedRecord.SourceType)
            .WithSessionId(storedRecord.SessionId)
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(signal)
            .WithContext(SignalContextTestBuilder.Create()
                .WithAuthentication(AuthenticationContextTestBuilder.Create()
                    .WithIdentity(IdentityTestBuilder.Create()
                        .WithIssuer(storedRecord.IdentityIssuer)
                        .WithSubject("different-subject")
                        .Build())
                    .Build())
                .Build())
            .Build();
        var repository = new FakeSessionRepository();
        await repository.SaveAsync(storedRecord);
        var configRepository = new FakeSignalSourceConfigRepository();
        await configRepository.SaveAsync(SignalSourceConfigTestBuilder.Create()
            .WithSourceType(signal.Source)
            .Build());
        var sut = UseCaseSutBuilder.Create()
            .WithRepository(repository)
            .WithConfigRepository(configRepository)
            .Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(envelope, new FakePresenter()))
            .To.Throw<GovernanceDeniedException>();
        Expect(await repository.LoadAsync(storedRecord.SessionId)).To.Equal(storedRecord);
    }

    [Test]
    public async Task Execute_WithNewSession_BindsCurrentIdentityAndExposesFreshContextToStrategy()
    {
        // Arrange
        var identity = IdentityTestBuilder.Create()
            .WithIssuer("issuer")
            .WithSubject("subject")
            .WithClaim("role", "operator")
            .Build();
        var authentication = AuthenticationContextTestBuilder.Create()
            .WithIdentity(identity)
            .WithAuthenticationMethod("oauth")
            .WithCredentialReference("calendar", "vault", "calendar-ref")
            .Build();
        var signal = SignalTestBuilder.Create().WithSource("cli").Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(signal)
            .WithContext(SignalContextTestBuilder.Create()
                .WithAuthentication(authentication)
                .WithAttribute("tenant", "contoso")
                .Build())
            .Build();
        var sourceConfig = SignalSourceConfigTestBuilder.Create()
            .WithSourceType(signal.Source)
            .Build();
        var repository = new FakeSessionRepository();
        var configRepository = new FakeSignalSourceConfigRepository();
        await configRepository.SaveAsync(sourceConfig);
        var scope = new FakeSignalContextScope();
        var strategy = new ContextCapturingStrategy(scope);
        var sut = UseCaseSutBuilder.Create()
            .WithStrategy(strategy)
            .WithRepository(repository)
            .WithConfigRepository(configRepository)
            .WithContextScope(scope)
            .Build();

        // Act
        await sut.ExecuteAsync(envelope, new FakePresenter());

        // Assert
        var record = repository.AllRecords().Single();
        Expect(record.IdentityIssuer).To.Equal(identity.Issuer);
        Expect(record.IdentitySubject).To.Equal(identity.Subject);
        Expect(strategy.Context?.SessionId).To.Equal(record.SessionId);
        Expect(strategy.Context?.Source).To.Equal(signal.Source);
        Expect(strategy.Context?.Authentication).To.Equal(authentication);
        Expect(strategy.Context?.Attributes["tenant"]).To.Equal("contoso");
        Expect(scope.Current).To.Be.Null();
    }

    [Test]
    public async Task Execute_WithContinuation_ExposesCurrentClaimsAndCredentialReferences()
    {
        // Arrange
        var identity = IdentityTestBuilder.Create()
            .WithIssuer("issuer")
            .WithSubject("subject")
            .WithClaim("role", "operator")
            .Build();
        var authentication = AuthenticationContextTestBuilder.Create()
            .WithIdentity(identity)
            .WithAuthenticationMethod("oauth")
            .WithCredentialReference("calendar", "vault", "current-calendar-ref")
            .Build();
        var storedRecord = SessionRecordTestBuilder.Create()
            .WithSessionId("sess-existing")
            .WithSourceType("cli")
            .WithIdentityIssuer(identity.Issuer)
            .WithIdentitySubject(identity.Subject)
            .Build();
        var signal = SignalTestBuilder.Create()
            .WithSource(storedRecord.SourceType)
            .WithSessionId(storedRecord.SessionId)
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create()
            .WithSignal(signal)
            .WithContext(SignalContextTestBuilder.Create()
                .WithAuthentication(authentication)
                .Build())
            .Build();
        var repository = new FakeSessionRepository();
        await repository.SaveAsync(storedRecord);
        var configRepository = new FakeSignalSourceConfigRepository();
        await configRepository.SaveAsync(SignalSourceConfigTestBuilder.Create()
            .WithSourceType(signal.Source)
            .Build());
        var scope = new FakeSignalContextScope();
        var strategy = new ContextCapturingStrategy(scope);
        var sut = UseCaseSutBuilder.Create()
            .WithRepository(repository)
            .WithConfigRepository(configRepository)
            .WithStrategy(strategy)
            .WithContextScope(scope)
            .Build();

        // Act
        await sut.ExecuteAsync(envelope, new FakePresenter());

        // Assert
        Expect(strategy.Context?.Authentication.Identity.GetClaimValues("role"))
            .To.Equal(new HashSet<string>(["operator"], StringComparer.Ordinal));
        Expect(strategy.Context?.Authentication.CredentialReferences["calendar"].Reference)
            .To.Equal("current-calendar-ref");
    }

    [Test]
    public async Task Execute_WhenStrategyThrows_UpdatesStatusToFailed()
    {
        // Arrange
        var signal = SignalTestBuilder.Create()
            .WithSource("cli")
            .Build();
        var envelope = SignalEnvelopeTestBuilder.Create().WithSignal(signal).Build();
        var sourceConfig = SignalSourceConfigTestBuilder.Create()
            .WithSourceType("cli")
            .WithProvider("ollama")
            .WithModelId("gemma4")
            .WithSystemPrompt("You are a helpful assistant.")
            .WithToolBelt(ToolBelt.Empty)
            .WithObservabilityMode("off")
            .Build();
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
        Expect(async () => await sut.ExecuteAsync(envelope, new FakePresenter()))
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
        var envelope = SignalEnvelopeTestBuilder.Create().WithSignal(signal).Build();
        var sut = UseCaseSutBuilder.Create().Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(envelope, new FakePresenter()))
            .To.Throw<InvalidOperationException>()
            .With.Message.Containing("unknown");
    }

    [Test]
    public void Execute_WithNullSignal_Throws()
    {
        // Arrange
        var sut = UseCaseSutBuilder.Create().Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(null!, new FakePresenter()))
            .To.Throw<ArgumentNullException>()
            .With.Message.Containing("envelope");
    }

    [Test]
    public void Execute_WithNullPresenter_Throws()
    {
        // Arrange
        var sut = UseCaseSutBuilder.Create().Build();
        var signal = SignalTestBuilder.Create().Build();
        var envelope = SignalEnvelopeTestBuilder.Create().WithSignal(signal).Build();

        // Act & Assert
        Expect(async () => await sut.ExecuteAsync(envelope, null!))
            .To.Throw<ArgumentNullException>()
            .With.Message.Containing("presenter");
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
    private ISignalContextScope _contextScope = new FakeSignalContextScope();

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

    public UseCaseSutBuilder WithContextScope(ISignalContextScope contextScope)
    {
        _contextScope = contextScope;
        return this;
    }

    public RunSessionUseCase Build() => new(_strategy, _repository, _configRepository, _timeProvider, _contextScope);
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
    public async Task<SessionResult> ExecuteAsync(AgentExecutionRequest request, ISignalPresenter presenter)
    {
        var updated = result with { SessionId = request.SessionId };
        await presenter.PresentAsync(updated);
        return updated;
    }
}

file sealed class FailingStrategy(Exception error) : IAgentStrategy
{
    public Task<SessionResult> ExecuteAsync(AgentExecutionRequest request, ISignalPresenter presenter)
        => throw error;
}

file sealed class ContextCapturingStrategy(FakeSignalContextScope scope) : IAgentStrategy
{
    public SignalExecutionContext? Context { get; private set; }

    public Task<SessionResult> ExecuteAsync(AgentExecutionRequest request, ISignalPresenter presenter)
    {
        Context = scope.Current;
        return Task.FromResult(SessionResultTestBuilder.Create()
            .WithSessionId(request.SessionId)
            .Build());
    }
}

file sealed class FakeSignalContextScope : ISignalContextScope
{
    public SignalExecutionContext? Current { get; private set; }

    public IDisposable Push(SignalExecutionContext context)
    {
        Current = context;
        return new Scope(this);
    }

    private sealed class Scope(FakeSignalContextScope owner) : IDisposable
    {
        public void Dispose() => owner.Current = null;
    }
}

file sealed class FakePresenter : ISignalPresenter
{
    public string? LastSessionId { get; private set; }
    public Exception? LastException { get; private set; }
    public bool ProcessingPresented { get; private set; }

    public Task PresentProcessingAsync(string sessionId, string? messageId = null)
    {
        ProcessingPresented = true;
        return Task.CompletedTask;
    }

    public Task PresentAsync(SessionResult result)
    {
        LastSessionId = result.SessionId;
        return Task.CompletedTask;
    }

    public Task PresentErrorAsync(string? sessionId, Exception exception)
    {
        LastSessionId = sessionId;
        LastException = exception;
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
