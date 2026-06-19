using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Ports;

public interface IAgentStrategy
{
    Task<SessionResult> ExecuteAsync(AgentSessionConfig config, Signal signal);
}
