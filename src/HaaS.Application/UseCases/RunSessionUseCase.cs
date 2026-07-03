using System.Text.Json;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public class RunSessionUseCase
{
    private readonly IAgentStrategy _agentStrategy;
    private readonly IExecutionTarget _executionTarget;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISignalSourceConfigRepository _signalSourceConfigRepository;
    private readonly TimeProvider _timeProvider;

    public RunSessionUseCase(
        IAgentStrategy agentStrategy,
        IExecutionTarget executionTarget,
        ISessionRepository sessionRepository,
        ISignalSourceConfigRepository signalSourceConfigRepository,
        TimeProvider timeProvider)
    {
        _agentStrategy = agentStrategy;
        _executionTarget = executionTarget;
        _sessionRepository = sessionRepository;
        _signalSourceConfigRepository = signalSourceConfigRepository;
        _timeProvider = timeProvider;
    }

    public async Task<string> ExecuteAsync(Signal signal)
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
                "running",
                config.Provider,
                config.ModelId,
                config.SystemPrompt,
                JsonSerializer.Serialize(config.ToolBelt),
                config.ThinkingLevel,
                now,
                now);
            await _sessionRepository.SaveAsync(record);
        }

        SessionResult result;
        try
        {
            result = await _agentStrategy.ExecuteAsync(signal, sessionId);
        }
        catch
        {
            var failed = await _sessionRepository.LoadAsync(sessionId);
            if (failed is not null)
            {
                failed = failed with
                {
                    Status = "failed",
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
                Status = "completed",
                UpdatedAt = _timeProvider.GetUtcNow()
            };
            await _sessionRepository.SaveAsync(updated);
        }

        await _executionTarget.DeliverAsync(result);
        return result.SessionId;
    }

    private async Task<string> ResolveSessionIdAsync(Signal signal)
    {
        if (signal.SessionId is not null)
        {
            var existing = await _sessionRepository.LoadAsync(signal.SessionId);
            if (existing is not null)
                return existing.SessionId;
        }

        return Guid.NewGuid().ToString();
    }
}
