using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public class RunSessionUseCase : IRunSessionUseCase
{
    private readonly IAgentStrategy _agentStrategy;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISignalSourceConfigRepository _signalSourceConfigRepository;
    private readonly TimeProvider _timeProvider;

    public RunSessionUseCase(
        IAgentStrategy agentStrategy,
        ISessionRepository sessionRepository,
        ISignalSourceConfigRepository signalSourceConfigRepository,
        TimeProvider timeProvider)
    {
        _agentStrategy = agentStrategy;
        _sessionRepository = sessionRepository;
        _signalSourceConfigRepository = signalSourceConfigRepository;
        _timeProvider = timeProvider;
    }

    public async Task<SessionResult> ExecuteAsync(Signal signal, ISignalPresenter presenter)
    {
        var sourceConfig = await _signalSourceConfigRepository.GetBySourceTypeAsync(signal.Source)
            ?? throw new InvalidOperationException($"No signal source config found for source type '{signal.Source}'.");
        var config = sourceConfig.ToSessionConfig();
        var sessionId = await ResolveSessionIdAsync(signal);
        var now = _timeProvider.GetUtcNow();

        if (await _sessionRepository.LoadAsync(sessionId) is null)
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
                now,
                now);
            await _sessionRepository.SaveAsync(record);
        }

        SessionResult result;
        try
        {
            result = await _agentStrategy.ExecuteAsync(signal, sessionId, presenter);
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
}
