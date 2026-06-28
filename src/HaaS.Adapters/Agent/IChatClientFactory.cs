using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public interface IChatClientFactory
{
    bool CanCreate(AgentSessionConfig config);
    IChatClient Create(AgentSessionConfig config);
}
