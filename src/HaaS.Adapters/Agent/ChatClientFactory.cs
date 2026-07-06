using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public sealed class ChatClientFactory : IChatClientFactory
{
    private readonly IProviderConfigRepository _configRepo;
    private readonly Dictionary<string, Func<ProviderConfig, string, IChatClient>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Action<ChatOptions, AgentSessionConfig>> _optionConfigurators = new(StringComparer.OrdinalIgnoreCase);

    public ChatClientFactory(IProviderConfigRepository configRepo)
    {
        _configRepo = configRepo;
    }

    public ChatClientFactory Register(string provider, Func<ProviderConfig, string, IChatClient> factory, Action<ChatOptions, AgentSessionConfig>? configureOptions = null)
    {
        _factories[provider] = factory;
        if (configureOptions is not null)
            _optionConfigurators[provider] = configureOptions;
        return this;
    }

    public bool CanCreate(string provider)
    {
        return _factories.ContainsKey(provider);
    }

    public async Task<IChatClient> CreateAsync(string provider, string modelId)
    {
        if (!_factories.TryGetValue(provider, out var factory))
        {
            throw new InvalidOperationException(
                $"No chat client factory registered for provider '{provider}'.");
        }

        var config = await _configRepo.GetAsync(provider);
        if (config is null)
        {
            throw new InvalidOperationException(
                $"No provider configuration found for '{provider}'.");
        }

        return factory(config, modelId);
    }

    public void ConfigureOptions(string provider, ChatOptions options, AgentSessionConfig config)
    {
        if (_optionConfigurators.TryGetValue(provider, out var configurator))
            configurator(options, config);
    }
}
