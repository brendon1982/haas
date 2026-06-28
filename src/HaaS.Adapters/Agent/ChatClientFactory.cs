using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public sealed class ChatClientFactory : IChatClientFactory
{
    private readonly Dictionary<string, Func<AgentSessionConfig, IChatClient>> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ChatClientFactory Register(string provider, Func<AgentSessionConfig, IChatClient> factory)
    {
        _providers[provider] = factory;
        return this;
    }

    public bool CanCreate(AgentSessionConfig config)
    {
        return _providers.ContainsKey(config.Provider);
    }

    public IChatClient Create(AgentSessionConfig config)
    {
        if (_providers.TryGetValue(config.Provider, out var factory))
            return factory(config);

        throw new InvalidOperationException(
            $"No chat client factory registered for provider '{config.Provider}'.");
    }
}
