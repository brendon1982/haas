using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public interface IChatClientFactory
{
    IChatClient Create(AgentSessionConfig config);
}
