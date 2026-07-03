using System.Collections.Concurrent;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Adapters.Store;

public class InMemorySignalSourceConfigRepository : ISignalSourceConfigRepository
{
    private readonly ConcurrentDictionary<string, SignalSourceConfig> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<SignalSourceConfig?> GetBySourceTypeAsync(string sourceType)
    {
        _store.TryGetValue(sourceType, out var config);
        return Task.FromResult<SignalSourceConfig?>(config);
    }

    public Task SaveAsync(SignalSourceConfig config)
    {
        _store[config.SourceType] = config;
        return Task.CompletedTask;
    }
}
