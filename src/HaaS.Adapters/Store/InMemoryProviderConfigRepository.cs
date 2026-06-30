using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class InMemoryProviderConfigRepository : IProviderConfigRepository
{
    private readonly ConcurrentDictionary<string, ProviderConfig> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ProviderConfig>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<ProviderConfig>>([.. _store.Values]);
    }

    public Task<ProviderConfig?> GetAsync(string provider)
    {
        _store.TryGetValue(provider, out var config);
        return Task.FromResult<ProviderConfig?>(config);
    }

    public Task SaveAsync(ProviderConfig config)
    {
        _store[config.Provider] = config;
        return Task.CompletedTask;
    }
}
