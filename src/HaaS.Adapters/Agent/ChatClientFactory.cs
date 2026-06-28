using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public sealed class ChatClientFactory : IChatClientFactory
{
    private readonly Func<AgentSessionConfig, IChatClient> _factory;

    public ChatClientFactory(Func<AgentSessionConfig, IChatClient> factory)
    {
        _factory = factory;
    }

    public IChatClient Create(AgentSessionConfig config) => _factory(config);
}
