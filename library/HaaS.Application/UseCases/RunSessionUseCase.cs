using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Domain.Exceptions;

namespace HaaS.Application.UseCases;

public class RunSessionUseCase : IRunSessionUseCase
{
    private readonly IAgentStrategy _agentStrategy;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISignalSourceConfigRepository _signalSourceConfigRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ISignalContextScope _signalContextScope;

    public RunSessionUseCase(
        IAgentStrategy agentStrategy,
        ISessionRepository sessionRepository,
        ISignalSourceConfigRepository signalSourceConfigRepository,
        TimeProvider timeProvider,
        ISignalContextScope signalContextScope)
    {
        _agentStrategy = agentStrategy;
        _sessionRepository = sessionRepository;
        _signalSourceConfigRepository = signalSourceConfigRepository;
        _timeProvider = timeProvider;
        _signalContextScope = signalContextScope;
    }

    public async Task<SessionResult> ExecuteAsync(SignalEnvelope envelope, ISignalPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(presenter);

        var signal = envelope.Signal;
        var sourceConfig = await _signalSourceConfigRepository.GetBySourceTypeAsync(signal.Source)
            ?? throw new InvalidOperationException($"No signal source config found for source type '{signal.Source}'.");
        var sessionId = await ResolveSessionIdAsync(signal);
        var now = _timeProvider.GetUtcNow();
        var existing = await _sessionRepository.LoadAsync(sessionId);

        if (existing is not null)
        {
            ValidateContinuation(existing, signal.Source, envelope.Context.Authentication.Identity, sessionId);
        }

        var config = existing?.ToConfig() ?? sourceConfig.ToSessionConfig();
        using var context = _signalContextScope.Push(new SignalExecutionContext(
            sessionId,
            signal.Source,
            envelope.Context.Authentication,
            envelope.Context.Attributes));

        if (existing is null)
        {
            var record = new SessionRecord(
                sessionId,
                signal.Source,
                SessionRecord.Statuses.Running,
                config.Provider,
                config.ModelId,
                config.SystemPrompt,
                System.Text.Json.JsonSerializer.Serialize(config.ToolBelt),
                config.ThinkingLevel,
                null,
                now,
                now,
                envelope.Context.Authentication.Identity.Issuer,
                envelope.Context.Authentication.Identity.Subject);
            await _sessionRepository.SaveAsync(record);
        }

        SessionResult result;
        try
        {
            result = await _agentStrategy.ExecuteAsync(
                new AgentExecutionRequest(signal, sessionId, config.ToolBelt.Tools),
                presenter);
        }
        catch
        {
            var failed = await _sessionRepository.LoadAsync(sessionId);
            if (failed is not null)
            {
                failed = failed with
                {
                    Status = SessionRecord.Statuses.Failed,
                    UpdatedAt = _timeProvider.GetUtcNow()
                };
                await _sessionRepository.SaveAsync(failed);
            }

            throw;
        }

        var updated = await _sessionRepository.LoadAsync(sessionId);
        if (updated is not null)
        {
            updated = updated with
            {
                Status = SessionRecord.Statuses.Completed,
                Output = result.Output,
                UpdatedAt = _timeProvider.GetUtcNow()
            };
            await _sessionRepository.SaveAsync(updated);
        }

        return result;
    }

    private Task<string> ResolveSessionIdAsync(Signal signal)
    {
        return Task.FromResult(signal.SessionId ?? Guid.NewGuid().ToString());
    }

    private static void ValidateContinuation(
        SessionRecord record,
        string source,
        Identity identity,
        string sessionId)
    {
        if (!StringComparer.Ordinal.Equals(record.SourceType, source))
        {
            throw new GovernanceDeniedException(sessionId, "SessionStart", "source-mismatch");
        }

        if (!StringComparer.Ordinal.Equals(record.IdentityIssuer, identity.Issuer)
            || !StringComparer.Ordinal.Equals(record.IdentitySubject, identity.Subject))
        {
            throw new GovernanceDeniedException(sessionId, "SessionStart", "identity-mismatch");
        }
    }
}
