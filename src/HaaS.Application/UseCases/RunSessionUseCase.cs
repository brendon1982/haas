using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public class RunSessionUseCase
{
    private readonly IAgentStrategy _agentStrategy;
    private readonly IExecutionTarget _executionTarget;

    public RunSessionUseCase(IAgentStrategy agentStrategy, IExecutionTarget executionTarget)
    {
        _agentStrategy = agentStrategy;
        _executionTarget = executionTarget;
    }

    public async Task<string> ExecuteAsync(AgentSessionConfig config, Signal signal)
    {
        var result = await _agentStrategy.ExecuteAsync(config, signal);
        await _executionTarget.DeliverAsync(result);
        return result.SessionId;
    }
}
